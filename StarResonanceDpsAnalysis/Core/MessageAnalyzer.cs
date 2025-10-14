using System;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;

using BlueProto;
using StarResonanceDpsAnalysis.Plugin;
using StarResonanceDpsAnalysis.Plugin.DamageStatistics;
using ZstdNet;
using StarResonanceDpsAnalysis.Core.test;
using Google.Protobuf.Collections;
using StarResonanceDpsAnalysis.Core.Module;
using StarResonanceDpsAnalysis.Forms; // Database synchronization

namespace StarResonanceDpsAnalysis.Core
{
    /// <summary>
    /// Message Analyzer
    /// Responsible for processing TCP data captured from the game, including decompression, Protobuf parsing, data synchronization, damage statistics, etc.
    /// </summary>
    public class MessageAnalyzer
    {
        /// <summary>
        /// Top-level message type handlers
        /// Key = Message type ID (lower 15 bits)
        /// Value = Corresponding parsing method
        /// </summary>
        private static readonly Dictionary<int, Action<ByteReader, bool>> MessageHandlers = new()
        {
            { 2, ProcessNotifyMsg },   // Notification message
            { 3, ProcessType3Message }, // Unknown large message (potential guild roster)
            { 6, ProcessFrameDown }    // Frame down message
        };

        /// <summary>
        /// Main entry point: Process a batch of TCP packets
        /// </summary>
        public static void Process(byte[] packets)
        {
           
                var packetsReader = new ByteReader(packets);
                while (packetsReader.Remaining > 0)
                {
                    // Packet header length check
                    if (!packetsReader.TryPeekUInt32BE(out uint packetSize)) break;
                    if (packetSize < 6) break;                           // Less than minimum length, invalid
                    if (packetSize > packetsReader.Remaining) break;     // Incomplete, wait for next packet

                    // Extract a complete packet by length
                    var packetReader = new ByteReader(packetsReader.ReadBytes((int)packetSize));
                    uint sizeAgain = packetReader.ReadUInt32BE();
                    if (sizeAgain != packetSize) continue; // Length mismatch, discard

                    // Read message type
                    var packetType = packetReader.ReadUInt16BE();
                    var isZstdCompressed = (packetType & 0x8000) != 0; // High bit 15 indicates compression
                    var msgTypeId = packetType & 0x7FFF;                // Lower 15 bits are the real type

                    // Console output for packet analysis - looking for guild roster info
                    if (packetSize > 10)
                    {
                        //Console.WriteLine($"[PACKET] Size: {packetSize}, Type: {msgTypeId}, Compressed: {isZstdCompressed}");
                        if (packetSize > 1000) // Only log large packets that might contain guild data
                        {
                            //Console.WriteLine($"[LARGE PACKET] Size: {packetSize}, Type: {msgTypeId}, Compressed: {isZstdCompressed}");
                            // Log first 64 bytes as hex for analysis
                            var headerBytes = packetReader.PeekBytes(Math.Min(64, (int)packetSize));
                            //onsole.WriteLine($"[HEADER] {BitConverter.ToString(headerBytes).Replace("-", " ")}");
                        }
                    }
                    
                    // Dispatch to corresponding handler method
                    if (!MessageHandlers.TryGetValue(msgTypeId, out var handler))
                    {
                        continue; // Unrecognized type, skip
                    }
                    handler(packetReader, isZstdCompressed);
                }
        
        }

        /// <summary>
        /// Player attribute type definitions
        /// Used for attribute ID parsing in SyncNearEntities messages
        /// </summary>
        public enum AttrType
        {
            /// <summary>Name (player/unit name, string)</summary>
            AttrName = 0x01,                    // name
            AttrId = 0x0A, // Integer (entity/monster ID, can map to name)
            /// <summary>Profession ID (for profession enum/configuration mapping)</summary>
            AttrProfessionId = 0xDC,            // profession id

            /// <summary>Combat Power (Fight Point/Combat Power, integer)</summary>
            AttrFightPoint = 0x272E,            // fight power

            /// <summary>Level</summary>
            AttrLevel = 0x2710,                 // level

            /// <summary>Rank Level (specific meaning according to game definition)</summary>
            AttrRankLevel = 0x274C,             // rank level

            /// <summary>Critical rate (unit determined by upstream, commonly ten-thousandths or thousandths)</summary>
            AttrCri = 0x2B66,                   // crit rate

            /// <summary>Lucky rate (unit determined by upstream, commonly ten-thousandths or thousandths)</summary>
            AttrLucky = 0x2B7A,                 // lucky rate

            /// <summary>Current HP</summary>
            AttrHp = 0x2C2E,                    // hp

            /// <summary>Maximum HP</summary>
            AttrMaxHp = 0x2C38,                 // max hp

            /// <summary>
            /// Element flag (element-related bit flags/masks, such as ice/lightning/fire, etc.; specific bit meanings parsed according to configuration table)
            /// </summary>
            AttrElementFlag = 0x646D6C,         // element flags (bitmask)

            /// <summary>
            /// Reduction/Vulnerability Level (Reduction Level, indicates the level of reduction effect received)
            /// </summary>
            AttrReductionLevel = 0x64696D,      // reduction/vulnerability level

            /// <summary>
            /// Reduction/Vulnerability Effect ID (used to distinguish source or specific effect entries)
            /// </summary>
            AttrReduntionId = 0x6F6C65,         // reduction effect id

            /// <summary>
            /// Energy flag (Energy Flag/Charge status, usually bit flags; specific definition according to protocol/configuration)
            /// </summary>
            AttrEnergyFlag = 0x543CD3C6         // energy flags (bitmask)
        }

        /// <summary>
        /// Damage source type (distinguishes between skill body, projectiles, Buff DoT, fall damage, etc.)
        /// </summary>
        public enum EDamageSource
        {
            /// <summary>Damage caused by skill body (such as melee/spell skill hits)</summary>
            EDamageSourceSkill = 0,

            /// <summary>Damage caused by bullets/projectiles (such as arrows, missiles)</summary>
            EDamageSourceBullet = 1,

            /// <summary>Periodic or effect-triggered damage from Buff/DoT/field effects</summary>
            EDamageSourceBuff = 2,

            /// <summary>Fall damage</summary>
            EDamageSourceFall = 3,

            /// <summary>Fake bullets/projectiles for internal triggering (server-side logic, not real bullets)</summary>
            EDamageSourceFakeBullet = 4,

            /// <summary>Other unclassified sources (fallback enum for compatibility)</summary>
            EDamageSourceOther = 100
        }


        /// <summary>
        /// Damage type
        /// </summary>
        public enum EDamageProperty
        {
            General = 0,
            Fire = 1,
            Water = 2,
            Electricity = 3,
            Wood = 4,
            Wind = 5,
            Rock = 6,
            Light = 7,
            Dark = 8,
            Count = 9,
        }

        /// <summary>
        /// Convert element enum to short label (with emoji icons).
        /// </summary>
        /// <param name="damageProperty">EDamageProperty enum value</param>
        /// <returns>Corresponding label string</returns>
        public static string GetDamageElement(int damageProperty)
        {
            switch (damageProperty)
            {
                case (int)EDamageProperty.General:
                    return "⚔️物";
                case (int)EDamageProperty.Fire:
                    return "🔥火";
                case (int)EDamageProperty.Water:
                    return "❄️冰";
                case (int)EDamageProperty.Electricity:
                    return "⚡雷";
                case (int)EDamageProperty.Wood:
                    return "🍀森";
                case (int)EDamageProperty.Wind:
                    return "💨风";
                case (int)EDamageProperty.Rock:
                    return "⛰️岩";
                case (int)EDamageProperty.Light:
                    return "🌟光";
                case (int)EDamageProperty.Dark:
                    return "🌑暗";
                case (int)EDamageProperty.Count:
                    return "❓？"; // Unknown/reserved
                default:
                    return "⚔️物";
            }
        }


        /// <summary>
        /// Notify message internal method table
        /// Key = methodId
        /// Value = Corresponding handler method
        /// </summary>
        private static readonly Dictionary<uint, Action<byte[]>> ProcessMethods = new()
        {
            { 0x00000006U, ProcessSyncNearEntities },        // Sync nearby player entities
            { 0x00000015U, ProcessSyncContainerData },       // Sync own complete container data
            { 0x00000016U, ProcessSyncContainerDirtyData },  // Sync own partial updates (dirty data)
            { 0x0000002EU, ProcessSyncToMeDeltaInfo },       // Sync incremental damage received by self
            { 0x0000002DU, ProcessSyncNearDeltaInfo }       // Sync nearby incremental damage            
        };

        /// <summary>
        /// Process Notify message (RPC with serviceUuid and methodId)
        /// </summary>
        public static void ProcessNotifyMsg(ByteReader packet, bool isZstdCompressed)
        {
            var serviceUuid = packet.ReadUInt64BE(); // Service UUID
            _ = packet.ReadUInt32BE(); // stubId (not used for now)
            var methodId = packet.ReadUInt32BE(); // Method ID
            
            // Log all Notify messages for analysis
            //Console.WriteLine($"[NOTIFY] ServiceUUID: 0x{serviceUuid:X16}, MethodID: 0x{methodId:X8}");
            
            if (serviceUuid != 0x0000000063335342UL) 
            {
                Console.WriteLine($"[NOTIFY] Non-combat service ignored: 0x{serviceUuid:X16}");
                return; // Not combat-related, ignore
            }

            byte[] msgPayload = packet.ReadRemaining();
            if (isZstdCompressed) msgPayload = DecompressZstdIfNeeded(msgPayload);
            
            // Log payload size for large messages (potential guild data)
            //Console.WriteLine($"[NOTIFY] Payload size: {msgPayload.Length} bytes, MethodID: 0x{methodId:X8}");
            if (msgPayload.Length > 500) // Log large payloads that might contain guild roster
            {
                //Console.WriteLine($"[LARGE NOTIFY] MethodID: 0x{methodId:X8}, Size: {msgPayload.Length} bytes");
                // Log first 32 bytes as hex
                var headerSize = Math.Min(32, msgPayload.Length);
                var headerBytes = new byte[headerSize];
                Array.Copy(msgPayload, 0, headerBytes, 0, headerSize);
                //Console.WriteLine($"[NOTIFY HEADER] {BitConverter.ToString(headerBytes).Replace("-", " ")}");
            }

            if (!ProcessMethods.TryGetValue(methodId, out var processMethod)) 
            {
                //Console.WriteLine($"[NOTIFY UNKNOWN METHOD] MethodID: 0x{methodId:X8}, Size: {msgPayload.Length} bytes");
                return;
            }
            processMethod(msgPayload);
        }

        /// <summary>
        /// Process Type 3 message (guild roster data - Protobuf)
        /// </summary>
        public static void ProcessType3Message(ByteReader packet, bool isZstdCompressed)
        {   
            // Read the entire payload first
            byte[] payload = packet.ReadRemaining();
            //Console.WriteLine($"[TYPE3] Payload size: {payload.Length} bytes");
            
            // Create a new ByteReader for the payload to parse it
            var payloadReader = new ByteReader(payload);
            
            try
            {
                // Parse the actual structure based on observed pattern
                var messageType = payloadReader.ReadUInt32BE();     // 00 00 00 01
                var sequenceNumber = payloadReader.ReadUInt32BE();   // 00 00 00 7C (124)
                var flags = payloadReader.ReadUInt32BE();           // 00 00 00 00

                Console.WriteLine($"[TYPE3] MessageType: {messageType}, Sequence: {sequenceNumber}, Flags: 0x{flags:X8}");
                
                // Read remaining payload after header (this is the Protobuf data)
                byte[] protobufData = payloadReader.ReadRemaining();
                //Console.WriteLine($"[TYPE3] Protobuf data size: {protobufData.Length} bytes");
                
                // Log first 128 bytes as hex for preview
                var headerSize = Math.Min(128, protobufData.Length);
                var headerBytes = new byte[headerSize];
                Array.Copy(protobufData, 0, headerBytes, 0, headerSize);
                Console.WriteLine($"[TYPE3 BYTES] {BitConverter.ToString(headerBytes).Replace("-", " ")}");
                
                // Also output as ASCII for text analysis
                StringBuilder asciiBuilder = new StringBuilder();
                foreach (byte b in headerBytes)
                {
                    // Only include printable ASCII characters
                    if (b >= 32 && b <= 126)
                    {
                        asciiBuilder.Append((char)b);
                    }
                    else
                    {
                        asciiBuilder.Append('.');
                    }
                }
                Console.WriteLine($"[TYPE3 ASCII] {asciiBuilder}");
                
                // Analyze the protobuf data with our new analyzer
                ProtobufAnalyzer.AnalyzeUnknownData(protobufData, "Type3 Message");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[TYPE3] Error parsing header: {ex.Message}");                
            }
        }

        /// <summary>
        /// Process FrameDown message (nested internal packets)
        /// </summary>
        public static void ProcessFrameDown(ByteReader reader, bool isZstdCompressed)
        {
            _ = reader.ReadUInt32BE(); // serverSequenceId
            if (reader.Remaining == 0) return;

            var nestedPacket = reader.ReadRemaining();
            if (isZstdCompressed) nestedPacket = DecompressZstdIfNeeded(nestedPacket);
            Process(nestedPacket); // Recursively parse internal messages
        }

        #region Zstd decompression logic
        private static readonly uint ZSTD_MAGIC = 0xFD2FB528;
        private static readonly uint SKIPPABLE_MAGIC_MIN = 0x184D2A50;
        private static readonly uint SKIPPABLE_MAGIC_MAX = 0x184D2A5F;

        /// <summary>
        /// Decompress if data contains Zstd frames, otherwise return as-is
        /// </summary>
        private static byte[] DecompressZstdIfNeeded(byte[] buffer)
        {
            if (buffer == null || buffer.Length < 4) return Array.Empty<byte>();
            int off = 0;
            while (off + 4 <= buffer.Length)
            {
                uint magic = BitConverter.ToUInt32(buffer, off);
                if (magic == ZSTD_MAGIC) break;
                if (magic >= SKIPPABLE_MAGIC_MIN && magic <= SKIPPABLE_MAGIC_MAX)
                {
                    if (off + 8 > buffer.Length) throw new InvalidDataException("Incomplete skippable frame header");
                    uint size = BitConverter.ToUInt32(buffer, off + 4);
                    if (off + 8 + size > buffer.Length) throw new InvalidDataException("Incomplete skippable frame data");
                    off += 8 + (int)size;
                    continue;
                }
                off++;
            }
            if (off + 4 > buffer.Length) return buffer;

            using var input = new MemoryStream(buffer, off, buffer.Length - off, writable: false);
            using var decoder = new DecompressionStream(input);
            using var output = new MemoryStream();

            const long MAX_OUT = 32L * 1024 * 1024; // Maximum decompression 32MB
            var temp = new byte[8192];
            long total = 0;
            int read;
            while ((read = decoder.Read(temp, 0, temp.Length)) > 0)
            {
                total += read;
                if (total > MAX_OUT) throw new InvalidDataException("Decompression result exceeds 32MB limit");
                output.Write(temp, 0, read);
            }
            return output.ToArray();
        }
        #endregion
        /// <summary>
        /// Sync nearby entities, player data
        /// </summary>
        /// <param name="playerUid"></param>
        /// <param name="attrs"></param>
        public static void processPlayerAttrs(ulong playerUid,RepeatedField<Attr> attrs)
        {     
            bool updated = false;
            string name = "";
            foreach (var attr in attrs)
            {
                if (attr.Id == 0 || attr.RawData == null || attr.RawData.Length == 0) continue;
                var reader = new Google.Protobuf.CodedInputStream(attr.RawData.ToByteArray());

                switch (attr.Id)
                {
                    case (int)AttrType.AttrName:
                        StatisticData._manager.SetNickname(playerUid, reader.ReadString());
                        updated = true;
                        break;
                    case (int)AttrType.AttrProfessionId:
                        StatisticData._manager.SetProfession(playerUid, GetProfessionNameFromId(reader.ReadInt32()));
                        updated = true;
                        break;
                    case (int)AttrType.AttrFightPoint:
                        StatisticData._manager.SetCombatPower(playerUid, reader.ReadInt32());
                        updated = true;
                        break;
                    case (int)AttrType.AttrLevel:
                        StatisticData._manager.SetAttrKV(playerUid, "level", reader.ReadInt32());
                        break;
                    case (int)AttrType.AttrRankLevel:
                        StatisticData._manager.SetAttrKV(playerUid, "rank_level", reader.ReadInt32());
                        break;
                    case (int)AttrType.AttrCri:
                        StatisticData._manager.SetAttrKV(playerUid, "cri", reader.ReadInt32());
                        break;
                    case (int)AttrType.AttrLucky:
                        StatisticData._manager.SetAttrKV(playerUid, "lucky", reader.ReadInt32());
                        break;
                    case (int)AttrType.AttrHp:
                        StatisticData._manager.SetAttrKV(playerUid, "hp", reader.ReadInt32());
                        break;
                    case (int)AttrType.AttrMaxHp:
                        _ = reader.ReadInt32();

                        break;
                    case (int)AttrType.AttrElementFlag:
                        _ = reader.ReadInt32();

                        break;
                    case (int)AttrType.AttrEnergyFlag:
                        _ = reader.ReadInt32();

                        break;
                    case (int)AttrType.AttrReductionLevel:
                        _ = reader.ReadInt32();

                        break;
                    default:
                        break;
                }
            }

 
        }

        public static void processEnemyAttrs(ulong enemyUid, RepeatedField<Attr> attrs)
        {
            #region
            //foreach (var attr in attrs)
            //{
            //    if (attr.Id == 0 || attr.RawData == null)
            //        continue;
            //    var reader = new Google.Protobuf.CodedInputStream(attr.RawData.ToByteArray());

            //   // Console.WriteLine(@$"Found attribute ID {attr.Id} for enemy E{enemyUid} raw data={Convert.ToBase64String(attr.RawData.ToByteArray())}");
            //    switch (attr.Id)
            //    {
            //        case (int)AttrType.AttrName:
            //            {
            //                // Monster name is directly a string
            //                string enemyName = reader.ReadString();

            //                Console.WriteLine($"Found monster name {enemyName}, corresponding ID {enemyUid}");
            //                break;
            //            }
            //        case (int)AttrType.AttrId:
            //            {
            //                // Monster template ID
            //                int templateId = reader.ReadInt32();
            //                string name = MonsterNameResolver.Instance.GetName(templateId);
            //                if(!string.IsNullOrEmpty(name))
            //                {
            //                    Console.WriteLine($"Monster name: {name}, corresponding template ID {templateId}");
            //                }

            //                break;
            //            }
            //        case (int)AttrType.AttrHp:
            //            {
            //                var data = attr.RawData.ToByteArray();
            //                if (data.Length == 0)
            //                {
            //                    //Console.WriteLine($"Monster {enemyUid} HP data is empty, skipping");
            //                    break;
            //                }
            //                int enemyHp = reader.ReadInt32();
                           
            //                //Console.WriteLine($"Found monster current HP {enemyHp}, corresponding enemy ID {enemyUid}"); 
            //                break;
            //            }
            //        case (int)AttrType.AttrMaxHp:
            //            {
            //                int enemyMaxHp = reader.ReadInt32();

            //                Console.WriteLine($"Found monster max HP {enemyMaxHp}, corresponding enemy ID {enemyUid}");
            //                break;
            //            }
            //        default:
            //            {
            //                // Unknown attributes silent, optional debug
            //                // this.logger.Debug($"Found unknown attrId {attr.Id} for E{enemyUid} {Convert.ToBase64String(attr.RawData)}");
            //                break;
            //            }
            //    }
            //}
            #endregion
        }

        /// <summary>
        /// Sync nearby entities - monsters and players
        /// </summary>
        public static void ProcessSyncNearEntities(byte[] payloadBuffer)
        {
            #region
            var syncNearEntities = SyncNearEntities.Parser.ParseFrom(payloadBuffer);
            if (syncNearEntities.Appear == null || syncNearEntities.Appear.Count == 0) return;

            foreach (var entity in syncNearEntities.Appear)
            {
                if (entity.EntType != EEntityType.EntChar) continue;
                ulong playerUid = Shr16((ulong)entity.Uuid); // Extract UID
               
                if (playerUid == 0) continue;

          

                var attrCollection = entity.Attrs;
                if (attrCollection?.Attrs == null) continue;
                switch(entity.EntType)
                {
                    case EEntityType.EntMonster:
                        processEnemyAttrs(playerUid, attrCollection.Attrs);
                        break;
                    case EEntityType.EntChar:
                        processPlayerAttrs(playerUid, attrCollection.Attrs);
                        break;
                    default:
                        break;
                }
               
        
            }
            #endregion
        }



        /// <summary>
        /// Sync nearby incremental damage (skills/damage from other characters in range)
        /// </summary>
        public static void ProcessSyncNearDeltaInfo(byte[] payloadBuffer)
        {
            var syncNearDeltaInfo = SyncNearDeltaInfo.Parser.ParseFrom(payloadBuffer);
            if (syncNearDeltaInfo.DeltaInfos == null || syncNearDeltaInfo.DeltaInfos.Count == 0) return;
            foreach (var aoiSyncDelta in syncNearDeltaInfo.DeltaInfos) ProcessAoiSyncDelta(aoiSyncDelta);
        }


        /// <summary>
        /// Process a skill damage/healing record
        /// </summary>
        public static void ProcessAoiSyncDelta(AoiSyncDelta delta)
        {
            if (delta == null) return;
            ulong targetUuidRaw = (ulong)delta.Uuid;
            if (targetUuidRaw == 0) return;
            bool isTargetPlayer = IsUuidPlayerRaw(targetUuidRaw);
            ulong targetUuid = Shr16(targetUuidRaw);
            var attrCollection = delta.Attrs;
            if (attrCollection?.Attrs != null)
            {
                if(isTargetPlayer)
                {
                    //Player
                    processPlayerAttrs(targetUuidRaw, attrCollection.Attrs);
                }
                else
                {
                    //Monster
                    processEnemyAttrs(targetUuid, attrCollection.Attrs);
                }
            }



                // SkillEffects: Skill-related effects included in this increment (damage/healing, etc.)

                var skillEffect = delta.SkillEffects;
            if (skillEffect?.Damages == null || skillEffect.Damages.Count == 0) return;


            foreach (var d in skillEffect.Damages)
            {
                long skillId = d.OwnerId;
                if (skillId == 0) continue;

                ulong attackerRaw = (ulong)(d.TopSummonerId != 0 ? d.TopSummonerId : d.AttackerUuid);
                if (attackerRaw == 0) continue;
                bool isAttackerPlayer = IsUuidPlayerRaw(attackerRaw);
                ulong attackerUuid = Shr16(attackerRaw);

                // Check if basic information is missing, try to supplement if missing
                if (isAttackerPlayer && attackerUuid != 0)
                {
                    var info = StatisticData._manager.GetPlayerBasicInfo(attackerUuid);
                }

                // Damage value
                long damageSigned = d.HasValue ? d.Value : (d.HasLuckyValue ? d.LuckyValue : 0L);
                if (damageSigned == 0) continue;
                ulong damage = (ulong)(damageSigned < 0 ? -damageSigned : damageSigned);

                // Flags
                bool isCrit = d.TypeFlag != null && ((d.TypeFlag & 1) == 1);
                bool isHeal = d.Type == EDamageType.Heal;
                var luckyValue = d.LuckyValue;
                bool isLucky = luckyValue != null && luckyValue != 0;
                ulong hpLessen = d.HasHpLessenValue ? (ulong)d.HpLessenValue : 0UL;

                // 1) Whether "caused" lucky (CauseLucky): TypeFlag bit 2
                bool isCauseLucky = d.TypeFlag != null && ((d.TypeFlag & 0b100) == 0b100);

                // 2) Whether Miss
                bool isMiss = d.HasIsMiss && d.IsMiss;

                // 3) Whether killed/target death
                bool isDead = d.HasIsDead && d.IsDead;

                // 4) Element label (convert d.Property to your existing label string)
                string damageElement = GetDamageElement((int)d.Property);

                // 5) Damage source (EDamageSource)
                int damageSource = (int)(d.HasDamageSource ? d.DamageSource : 0);


                // Piling mode (only count damage to specific targets by self)
                if (AppConfig.PilingMode)
                {
                    if (attackerUuid != AppConfig.Uid) continue;
                    if (targetUuid != 75) continue;
                }
                
                // Distinguish whether target is a player
                if (isTargetPlayer)
                {
                    if (isHeal)
                    {

                            StatisticData._manager.AddHealing(isAttackerPlayer?attackerUuid:0, (ulong)skillId, damageElement, hpLessen, isCrit, isLucky, isCauseLucky, targetUuid);
                        
                  
                       
                    }
                    else
                    {
                       
                        StatisticData._manager.AddTakenDamage(targetUuid, (ulong)skillId, damage, damageSource, isMiss, isDead, isCrit, isLucky, hpLessen);
                    }
                }
                else
                {
                    if (!isHeal && isAttackerPlayer)
                    {
                    
                        StatisticData._manager.AddDamage(attackerUuid, (ulong)skillId, damageElement, damage, isCrit, isLucky, isCauseLucky, hpLessen);
                    }
                    //if (AppConfig.NpcsTakeDamage)
                    //{
               
                    StatisticData._npcManager.AddNpcTakenDamage(targetUuid, attackerUuid, skillId, damage, isCrit, isLucky, hpLessen, isMiss, isDead);
                        
                        
                    //}
                }
            }
        }

        /// <summary>
        /// Current user UUID
        /// </summary>
        public static long currentUserUuid = 0;

        /// <summary>
        /// Sync own incremental damage
        /// </summary>
        public static void ProcessSyncToMeDeltaInfo(byte[] payloadBuffer)
        {
            var syncToMeDeltaInfo = SyncToMeDeltaInfo.Parser.ParseFrom(payloadBuffer);
            var aoiSyncToMeDelta = syncToMeDeltaInfo.DeltaInfo;
            long uuid = aoiSyncToMeDelta.Uuid;
            if (uuid != 0 && currentUserUuid != uuid)
            {
                currentUserUuid = uuid;
            }
            var aoiSyncDelta = aoiSyncToMeDelta.BaseDelta;
            if (aoiSyncDelta == null) return;
            ProcessAoiSyncDelta(aoiSyncDelta);
        }


        public static byte[] PayloadBuffer = new byte[0];
        /// <summary>
        /// Sync own complete container data (basic attributes, nickname, profession, combat power)
        /// </summary>
        public static void ProcessSyncContainerData(byte[] payloadBuffer)
        {
            if(FormManager.moduleCalculationForm != null&& !FormManager.moduleCalculationForm.IsDisposed)
            {
                PayloadBuffer = payloadBuffer;
                
            }
           
            //Console.WriteLine("Head (first 64 bytes): " + ToHex(payloadBuffer));
            var syncContainerData = SyncContainerData.Parser.ParseFrom(payloadBuffer);
            if (syncContainerData?.VData == null) return;

            var vData = syncContainerData.VData;
            if (vData.CharId == null || vData.CharId == 0) return;

            ulong playerUid = (ulong)vData.CharId;
            AppConfig.Uid = playerUid;
            bool updated = false;

            if (vData.RoleLevel?.Level != 0)
                StatisticData._manager.SetAttrKV(playerUid, "level", vData.RoleLevel.Level);

            if (vData.Attr?.CurHp != 0)
                StatisticData._manager.SetAttrKV(playerUid, "hp", (int)vData.Attr.CurHp);

            if (vData.Attr?.MaxHp != 0)
                StatisticData._manager.SetAttrKV(playerUid, "max_hp", (int)vData.Attr.MaxHp);
       
            if (vData.CharBase != null)
            {
                if (!string.IsNullOrEmpty(vData.CharBase.Name))
                {
                    StatisticData._manager.SetNickname(playerUid, vData.CharBase.Name);
                    AppConfig.NickName = vData.CharBase.Name;
                    updated = true;
                }

                if (vData.CharBase.FightPoint != 0)
                {
                    StatisticData._manager.SetCombatPower(playerUid, vData.CharBase.FightPoint);
                    AppConfig.CombatPower = vData.CharBase.FightPoint;
                    updated = true;
                }

            }

            var professionList = vData.ProfessionList;
            if (professionList != null && professionList.CurProfessionId != 0)
            {
                var professionName = GetProfessionNameFromId(professionList.CurProfessionId);
                AppConfig.Profession = professionName;
                updated = true;
            }


        }


        /// <summary>
        /// Sync own partial updates (dirty data) // Incremental update, update when data is available
        /// </summary>
        public static void ProcessSyncContainerDirtyData(byte[] payloadBuffer)
        {
            try
            {
                if (currentUserUuid == 0) return;
                var dirty = SyncContainerDirtyData.Parser.ParseFrom(payloadBuffer);
                if (dirty?.VData?.BufferS == null || dirty.VData.BufferS.Length == 0) return;

                var buf = dirty.VData.BufferS.ToByteArray();
                using var ms = new MemoryStream(buf, writable: false);
                using var br = new BinaryReader(ms);

                if (!DoesStreamHaveIdentifier(br)) return;

                uint fieldIndex = br.ReadUInt32();
                _ = br.ReadInt32();

                ulong playerUid = (ulong)currentUserUuid >> 16;
                bool updated = false;

                switch (fieldIndex)
                {
                    case 2: // Name and combat power
                        {
                            if (!DoesStreamHaveIdentifier(br)) break;
                            fieldIndex = br.ReadUInt32();
                            _ = br.ReadInt32();
                            switch (fieldIndex)
                            {
                                case 5: // Name
                                    {
                                        string playerName = StreamReadString(br);
                                        if (!string.IsNullOrEmpty(playerName))
                                        {
                                            StatisticData._manager.SetNickname(playerUid, playerName);
                                            AppConfig.NickName = playerName;
                                            updated = true;
                                        }
                                        break;
                                    }
                                case 35: // Combat power
                                    {
                                        uint fightPoint = br.ReadUInt32();
                                        _ = br.ReadInt32();
                                        if (fightPoint != 0)
                                        {
                                            StatisticData._manager.SetCombatPower(playerUid, (int)fightPoint);
                                            AppConfig.CombatPower = (int)fightPoint;
                                            updated = true;
                                        }
                                        break;
                                    }
                            }
                            break;
                        }
                    case 16: // HP
                        {
                            if (!DoesStreamHaveIdentifier(br)) break;
                            fieldIndex = br.ReadUInt32();
                            _ = br.ReadInt32();
                            switch (fieldIndex)
                            {
                                case 1: // Current HP
                                    {
                                        uint curHp = br.ReadUInt32();
                                        StatisticData._manager.SetAttrKV(playerUid, "hp", (int)curHp);
                                        break;
                                    }
                                case 2: // Maximum HP
                                    {
                                        uint maxHp = br.ReadUInt32();
                                        StatisticData._manager.SetAttrKV(playerUid, "max_hp", (int)maxHp);
                                        break;
                                    }
                            }
                            break;
                        }
                    case 61: // Profession
                        {
                            if (!DoesStreamHaveIdentifier(br)) break;
                            fieldIndex = br.ReadUInt32();
                            _ = br.ReadInt32();
                            if (fieldIndex == 1)
                            {
                                uint curProfessionId = br.ReadUInt32();
                                _ = br.ReadInt32();
                                if (curProfessionId != 0)
                                {
                                    var professionName = GetProfessionNameFromId((int)curProfessionId);
                                    AppConfig.Profession = professionName;
                                    StatisticData._manager.SetProfession(playerUid, professionName);
                                    updated = true;
                                }
                            }
                            break;
                        }
                }


            }
            catch { }
        }

        /// <summary>
        /// Check if data stream still has identifier
        /// </summary>
        private static bool DoesStreamHaveIdentifier(BinaryReader br)
        {
            var s = br.BaseStream;

            // First ensure at least 8 bytes can be read (uint32 + int32)
            if (s.Position + 8 > s.Length) return false;

            uint id1 = br.ReadUInt32();  // Expected 0xFFFFFFFE
            int guard1 = br.ReadInt32(); // Following placeholder/length (consumed regardless)

            if (id1 != 0xFFFFFFFE)
            {
                // Same as JS: return false if first segment is wrong (already advanced 8 bytes at this point)
                return false;
            }

            // After passing first segment validation, read next 8 bytes
            if (s.Position + 8 > s.Length) return false;

            int id2 = br.ReadInt32();    // Ideally 0xFFFFFFFD (i.e., -3)
            int guard2 = br.ReadInt32(); // Placeholder/reserved

            // JS code doesn't enforce validation of id2, so return true directly here
            return true;
        }


        /// <summary>
        /// Read string from stream (with 4-byte alignment)
        /// </summary>
        private static string StreamReadString(BinaryReader br)
        {
            uint length = br.ReadUInt32();  // uint32LE
            _ = br.ReadInt32();             // guard (placeholder/length, consumed regardless)

            // Even if length is 0, read trailing guard to maintain consistency with JS behavior
            byte[] bytes = length > 0 ? br.ReadBytes((int)length) : Array.Empty<byte>();

            _ = br.ReadInt32();             // guard (placeholder/reserved)

            return bytes.Length == 0 ? string.Empty : Encoding.UTF8.GetString(bytes);
        }


        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        static bool IsUuidPlayerRaw(ulong uuidRaw) => (uuidRaw & 0xFFFFUL) == 640UL; // UUID lower 16 bits identify player

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        static ulong Shr16(ulong v) => v >> 16; // Right shift 16 bits to get player UID

        /// <summary>
        /// Map profession ID to profession name
        /// </summary>
        public static string GetProfessionNameFromId(int professionId) => professionId switch
        {
            1 => Properties.Strings.Profession_Stormblade,
            2 => Properties.Strings.Profession_FrostMage,
            3 => Properties.Strings.Profession_PurifyingAxe,
            4 => Properties.Strings.Profession_WindKnight,
            5 => Properties.Strings.Profession_VerdantOracle,
            9 => Properties.Strings.Profession_HeavyGuardian,
            11 => Properties.Strings.Profession_Marksman,
            12 => Properties.Strings.Profession_AegisKnight,
            8 => Properties.Strings.Profession_ThunderHandCannon,
            10 => Properties.Strings.Profession_DarkSpiritDance,
            13 => Properties.Strings.Profession_SoulMusician,
            _ => string.Empty,
        };
    }
}
