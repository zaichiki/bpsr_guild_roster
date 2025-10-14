using PacketDotNet;
using SharpPcap;
using StarResonanceDpsAnalysis.Extends;
using StarResonanceDpsAnalysis.Plugin.DamageStatistics;
using System.Buffers.Binary;
using System.Collections.Concurrent;
using System.Runtime.CompilerServices;

namespace StarResonanceDpsAnalysis.Core
{
    public class PacketAnalyzer()
    {
        #region ====== Constant Definitions ======
        // === Timeout and Gap Handling ===
        private readonly TimeSpan IdleTimeout = TimeSpan.FromSeconds(10);  // Reset if current stream has no packets for over 10s
        private readonly TimeSpan GapTimeout = TimeSpan.FromSeconds(2);   // Force alignment if waiting for gap fragments for over 2s

        private DateTime LastAnyPacketAt = DateTime.MinValue;  // Time when "last packet in any direction" was received for current identified stream
        private DateTime? WaitingGapSince = null;              // Start timestamp when waiting for gap fragments


        /// <summary>
        /// Server signature
        /// </summary>
        /// <remarks>
        /// Not sure if this is the server signature, in StarResonanceDamageCounter, there are comments like //c3SB?? following it
        /// </remarks>
        private readonly byte[] ServerSignature = [0x00, 0x63, 0x33, 0x53, 0x42, 0x00];

        /// <summary>
        /// Login return packet signature when switching servers
        /// </summary>
        private readonly byte[] LoginReturnSignature =
        [
            0x00, 0x00, 0x00, 0x62,
            0x00, 0x03,
            0x00, 0x00, 0x00, 0x01,
            0x00, 0x11, 0x45, 0x14, // seq?
            0x00, 0x00, 0x00, 0x00,
            0x0a, 0x4e, 0x08, 0x01, 0x22, 0x24
        ];

        #endregion

        #region ====== Public Properties and State ======

        /// <summary>
        /// Currently connected server address
        /// </summary>
        public string CurrentServer { get; set; } = string.Empty;
        /// <summary>
        /// Expected next TCP sequence number
        /// </summary>
        private uint? TcpNextSeq { get; set; } = null;
        /// <summary>
        /// TCP fragment cache
        /// </summary>
        /// <remarks>
        /// Key is TCP sequence number, Value is corresponding fragment data, used for reassembling multi-segment TCP data streams (e.g., a complete protobuf message split across multiple packets)
        /// </remarks>
        private ConcurrentDictionary<uint, byte[]> TcpCache { get; } = new();
        private DateTime TcpLastTime { get; set; } = DateTime.MinValue;
        private object TcpLock { get; } = new();

        private MemoryStream TcpStream { get; } = new();
        private ConcurrentDictionary<uint, DateTime> TcpCacheTime { get; } = new();


        #endregion

        #region ========== Start New Analysis ==========

        public void StartNewAnalyzer(ICaptureDevice device, RawCapture raw)
        {
            Task.Run(() =>
            {
                try
                {
                    HandleRaw(device, raw);
                }
                catch (Exception ex)
                {
                    var taskIdStr = (Task.CurrentId?.ToString() ?? "?") + ' ';
                    Console.WriteLine($"""

                        ==== ThreadID: {taskIdStr.PadRight(8, '=')}==============
                        Critical crash during packet analysis: {ex.Message}
                        {ex.StackTrace}
                        =======================

                        """);
                }
            });
        }

        #endregion

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void ForceReconnect(string reason)
        {
            Console.WriteLine($"[PacketAnalyzer] Reconnect due to {reason} @ {DateTime.Now:HH:mm:ss}");
            ResetCaptureState(); // Clear state, let subsequent packets re-run "server identification" logic
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void ForceResyncTo(uint seq)
        {
            Console.WriteLine($"[PacketAnalyzer] Resync to seq={seq}");
            TcpCache.Clear();
            TcpStream.Position = 0;
            TcpStream.SetLength(0); // Completely discard current unaligned data
            TcpNextSeq = seq;       // Restart accumulation from this fragment
            WaitingGapSince = null;
            TcpLastTime = DateTime.Now;
        }


        #region ========== Stage 1: Per-Packet Parsing Entry ==========
        // Unified TCP sequence number comparison (considering 32-bit wraparound)
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        static int SeqCmp(uint a, uint b) => unchecked((int)(a - b)); // a>=b => 非负；a<b（跨回绕）=> 负


        /// <summary>
        /// Handle a single data packet
        /// </summary>
        /// <param name="packetObj">Data packet object</param>
        private void HandleRaw(ICaptureDevice? device, RawCapture raw)
        {
            try
            {
                // Use PacketDotNet to parse into generic packet object (including Ethernet/IP/TCP etc.)
                var packet = Packet.ParsePacket(raw.LinkLayerType, raw.Data);

                // Extract TCP packet (returns null if not TCP)
                var tcpPacket = packet.Extract<TcpPacket>();
                if (tcpPacket == null) return;

                // Extract IPv4 packet (also returns null if not IPv4)
                var ipv4Packet = packet.Extract<IPv4Packet>();
                if (ipv4Packet == null) return;

                // Get TCP payload (i.e., application layer data)
                var payload = tcpPacket.PayloadData;
                if (payload == null || payload.Length == 0) return;

                // Construct source -> destination IP and port string for current packet as unique identifier
                var srcServer = $"{ipv4Packet.SourceAddress}:{tcpPacket.SourcePort} -> {ipv4Packet.DestinationAddress}:{tcpPacket.DestinationPort}";
                var revServer = $"{ipv4Packet.DestinationAddress}:{tcpPacket.DestinationPort} -> {ipv4Packet.SourceAddress}:{tcpPacket.SourcePort}";
                var now = DateTime.Now;
                lock (TcpLock)
                {
                    // === Idle timeout for existing stream: Reset directly if no packets in either direction for a long time, allowing server re-identification ===
                    if (!string.IsNullOrEmpty(CurrentServer))
                    {
                        if (CurrentServer == srcServer || CurrentServer == revServer)
                            LastAnyPacketAt = now;

                        if (LastAnyPacketAt != DateTime.MinValue && (now - LastAnyPacketAt) > IdleTimeout)
                        {
                            ForceReconnect("idle timeout (no packets for current flow)");
                            // Continue processing, let new packets have a chance to be identified as new server
                        }
                    }

                    if (CurrentServer != srcServer)
                    {
                        try
                        {
                            // Try to identify server through small packets
                            if (payload.Length > 10 && payload[4] == 0)
                            {
                                var data = payload.AsSpan(10);
                                if (data.Length > 0)
                                {
                                    using var payloadMs = new MemoryStream(data.ToArray());
                                    byte[] tmp;
                                    do
                                    {
                                        var lenBuffer = new byte[4];
                                        if (payloadMs.Read(lenBuffer, 0, 4) != 4)
                                            break;

                                        var len = lenBuffer.ReadInt32BigEndian();
                                        if (len < 4 || len > payloadMs.Length - 4)
                                        {
                                            break;
                                        }

                                        tmp = new byte[len - 4];
                                        if (payloadMs.Read(tmp, 0, tmp.Length) != tmp.Length)
                                        {
                                            break;
                                        }

                                        if (!tmp.Skip(5).Take(ServerSignature.Length).SequenceEqual(ServerSignature))
                                        {
                                            break;
                                        }

                                        try
                                        {
                                            if (CurrentServer != srcServer)
                                            {
                                                CurrentServer = srcServer;
                                                ClearTcpCache();
                                                TcpNextSeq = tcpPacket.SequenceNumber + (uint)payload.Length;
                                                Console.WriteLine($"Got Scene Server Address: {srcServer}");
                                            }
                                        }
                                        catch (Exception ex)
                                        {
                                            Console.WriteLine($"""

                                                =======================
                                                HandleRaw 检测场景服务器时遇到关键性崩溃: {ex.Message}
                                                {ex.StackTrace}
                                                =======================

                                                """);
                                        }
                                    }
                                    while (tmp.Length > 0);
                                }
                            }

                            // Try to identify server through login return packet (still needs testing)
                            if (payload.Length == 0x62)
                            {
                                if (payload.AsSpan(0, 10).SequenceEqual(LoginReturnSignature.AsSpan(0, 10)) &&
                                    payload.AsSpan(14, 6).SequenceEqual(LoginReturnSignature.AsSpan(14, 6)))
                                {
                                    if (CurrentServer != srcServer)
                                    {
                                        CurrentServer = srcServer;
                                        ClearTcpCache();
                                        TcpNextSeq = tcpPacket.SequenceNumber + (uint)payload.Length;
                                        FullRecord.Reset();//Clear full journey statistics data
                                        Console.WriteLine($"Got Scene Server Address by Login Return Packet: {srcServer}");
                                    }
                                }
                            }
                        }
                        catch (Exception ex)
                        {
                            Console.WriteLine($"""

                                =======================
                                HandleRaw 中遇到关键性崩溃: {ex.Message}
                                {ex.StackTrace}
                                =======================

                                """);
                        }

                        return;
                    }
                    // This is already a packet from the identified server

                    if (TcpNextSeq == null)
                    {
                        Console.WriteLine("Unexpected TCP capture error! tcp_next_seq is NULL");
                        if (payload.Length > 4 && BinaryPrimitives.ReadUInt32BigEndian(payload) < 0x0fffff)
                        {
                            TcpNextSeq = tcpPacket.SequenceNumber;
                        }
                    }
                    // === Gap detection: New fragment sequence number > expected sequence number, indicates missing packets ===
                    if (TcpNextSeq != null)
                    {
                        int cmp = SeqCmp(tcpPacket.SequenceNumber, TcpNextSeq.Value);
                        if (cmp > 0) // Gap ahead, wait a bit
                        {
                            WaitingGapSince ??= now;
                            if ((now - WaitingGapSince.Value) > GapTimeout)
                            {
                                // Timeout still no missing fragments, directly realign from current fragment
                                ForceResyncTo(tcpPacket.SequenceNumber);
                            }
                        }
                        else if (cmp == 0)
                        {
                            // Normal sequential arrival, clear gap waiting
                            WaitingGapSince = null;
                        }
                        else
                        {
                            // cmp < 0: Old/duplicate fragment, ignore as needed (no additional action required)
                        }
                    }
                    // Caching strategy: Only accept "current/future" segments (avoid duplicate old segments occupying memory)
                    if (TcpNextSeq == null || SeqCmp(tcpPacket.SequenceNumber, TcpNextSeq.Value) >= 0)
                    {
                        TcpCache[tcpPacket.SequenceNumber] = payload.ToArray(); // Note: holding a copy
                        //ScheduleEvictAfter(tcpPacket.SequenceNumber);
                    }

                    // Sequentially concatenate to a temporary buffer (reduce multiple ToArray calls)
                    using var messageMs = new MemoryStream(capacity: 4096);
                  

                    while (TcpNextSeq != null && TcpCache.Remove(TcpNextSeq.Value, out var cachedTcpData))
                    {
                        messageMs.Write(cachedTcpData, 0, cachedTcpData.Length);
                        unchecked { TcpNextSeq += (uint)cachedTcpData.Length; }
                        TcpLastTime = now;            // <== Update "last concatenation time"
                        LastAnyPacketAt = now;        // <== As long as concatenation succeeds, consider stream as active
                    }

                    // Directly "append" concatenated bytes to TcpStream end, avoid intermediate copies from CopyTo
                    if (messageMs.Length > 0)
                    {
                        long endPos = TcpStream.Length;
                        TcpStream.Position = endPos;
                        messageMs.Position = 0;
                        messageMs.CopyTo(TcpStream);
                    }

                    // Parse: Set Position to current unprocessed starting point (i.e., 0), loop to get packets
                    TcpStream.Position = 0;

                    Span<byte> lenBuf = stackalloc byte[4];
                    while (true)
                    {
                        long start = TcpStream.Position;
                        if (TcpStream.Length - start < 4) break; // Insufficient 4-byte length header

                        int n = TcpStream.Read(lenBuf);
                        if (n < 4) { TcpStream.Position = start; break; }

                        int packetSize = BinaryPrimitives.ReadInt32BigEndian(lenBuf);

                        // Protocol agreement: Length is "total length (including 4B header)"
                        if (packetSize <= 4 || packetSize > 0x0FFFFF)
                        {
                            TcpStream.Position = start; // Rollback, leave for upper layer/subsequent processing
                            break;
                        }

                        if (TcpStream.Length - start < packetSize)
                        {
                            TcpStream.Position = start; // Packet incomplete, don't consume any bytes
                            break;
                        }

                        // Complete packet: read [4B length header + payload]
                        TcpStream.Position = start;
                        byte[] messagePacket = new byte[packetSize];
                        int read = TcpStream.Read(messagePacket, 0, packetSize);
                        if (read != packetSize) { TcpStream.Position = start; break; }

                        MessageAnalyzer.Process(messagePacket);
                    }

                    // Compact remaining unparsed data to stream head (zero-copy to minimize temporary arrays)
                    if (TcpStream.Position > 0)
                    {
                        long remain = TcpStream.Length - TcpStream.Position;
                        if (remain > 0)
                        {
                            // Move remaining data to front
                            var buffer = TcpStream.GetBuffer(); // 需要确保 TcpStream 是可公开缓冲的 MemoryStream
                            Buffer.BlockCopy(buffer, (int)TcpStream.Position, buffer, 0, (int)remain);
                            TcpStream.Position = 0;
                            TcpStream.SetLength(remain);
                        }
                        else
                        {
                            TcpStream.SetLength(0);
                            TcpStream.Position = 0;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                // Catch exceptions to avoid program crash, while printing exception information
                Console.WriteLine($"Packet processing exception: {ex.Message}\r\n{ex.StackTrace}");
            }
        }


        public void ResetCaptureState()
        {
            lock (TcpLock)
            {
                CurrentServer = string.Empty;      // Or null, depending on your judgment logic
                TcpNextSeq = null;
                TcpLastTime = DateTime.MinValue;

                TcpCache.Clear();
                TcpCacheTime.Clear();
             
                // If previous stream becomes too large, directly discard and replace to save memory
                if (TcpStream.Capacity > 1 << 20)  // >1MB 就换新，阈值自定
                {
                    // Clear and optionally shrink capacity, avoid reusing disposed stream
                    TcpStream.Position = 0;
                    TcpStream.SetLength(0);
                    // 如果需要 GetBuffer，确保用可公开缓冲的构造
                    typeof(MemoryStream)
                        .GetMethod("Dispose", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)?
                        .Invoke(TcpStream, null); // Can ignore: just ensure release
                }
                else
                {
                    TcpStream.Position = 0;
                    TcpStream.SetLength(0);
                }

            }
        }

        #endregion

        #region ====== TCP Cache Cleanup ======
        /// <summary>
        /// Clear TCP cache, used for reset operations in cases like disconnection, error reassembly, etc.
        /// </summary>
        private void ClearTcpCache()
        {
            TcpNextSeq = null;
            TcpLastTime = DateTime.MinValue;
            TcpCache.Clear();
        }
        #endregion
    }

}
