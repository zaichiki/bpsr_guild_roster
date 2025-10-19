# Google Sheets Integration Setup

This guide will help you set up Google Sheets integration for the Guild Roster export feature using OAuth 2.0 authentication.

## Prerequisites

1. A Google account
2. Access to Google Cloud Console
3. A Google Sheets document where you want the data to be exported

## Setup Steps

### 1. Create a Google Cloud Project

1. Go to [Google Cloud Console](https://console.cloud.google.com/)
2. Create a new project or select an existing one
3. Note down your project ID

### 2. Enable Google Sheets API

1. In the Google Cloud Console, go to "APIs & Services" > "Library"
2. Search for "Google Sheets API"
3. Click on it and press "Enable"

### 3. Create OAuth 2.0 Credentials

1. Go to "APIs & Services" > "Credentials"
2. Click "Create Credentials" > "OAuth client ID"
3. If prompted, configure the OAuth consent screen first:
   - Choose "External" user type
   - Fill in the required fields (App name, User support email, Developer contact)
   - Add your email to test users
   - Save and continue through the steps
4. For Application type, choose "Desktop application"
5. Give it a name like "Guild Roster Exporter"
6. Click "Create"
7. Copy the **Client ID** and **Client Secret** from the popup dialog

### 4. Create a Google Sheets Document

1. Go to [Google Sheets](https://sheets.google.com/)
2. Create a new spreadsheet
3. Name it something like "Guild Roster"
4. Copy the document ID from the URL (the long string between `/d/` and `/edit`)
   - Example: `https://docs.google.com/spreadsheets/d/1ABC123DEF456GHI789JKL/edit`
   - Document ID: `1ABC123DEF456GHI789JKL`

### 5. Configure the Application

1. Copy `private_config.ini.template` to `private_config.ini`
2. Open `private_config.ini` in a text editor
3. Find the `[GoogleSheets]` section
4. Set the following values:
   ```
   [GoogleSheets]
   ClientId=YOUR_CLIENT_ID_HERE
   ClientSecret=YOUR_CLIENT_SECRET_HERE
   DocumentId=YOUR_DOCUMENT_ID_HERE
   SheetName=Guild Roster
   ```
   
   **Note**: The `private_config.ini` file is excluded from version control for security reasons.

### 6. First-Time Authentication

1. Run the application and click "Export to Spreadsheet"
2. A web browser will open asking you to sign in to Google
3. Sign in with your Google account
4. Grant permission to the application to access your Google Sheets
5. The authentication will be saved for future use

## Usage

Once configured, you can use the "Export to Spreadsheet" button in the Guild Roster form to export data directly to your Google Sheet.

## Troubleshooting

### Common Issues

1. **"OAuth credentials are not configured"**
   - Make sure you've set both `ClientId` and `ClientSecret` in `config.ini`

2. **"Document ID not configured"**
   - Check that you've set the `DocumentId` in `config.ini`

3. **"Sheet not found"**
   - Make sure the sheet name in `config.ini` matches exactly with a sheet in your Google Sheets document

4. **"Authentication failed"**
   - Verify that the Google Sheets API is enabled in your Google Cloud project
   - Check that your OAuth credentials are correct
   - Make sure you've completed the OAuth consent screen configuration
   - Ensure your Google account has access to the Google Sheet

5. **"Access denied"**
   - Make sure you're signed in with a Google account that has access to the Google Sheet
   - Check that the OAuth consent screen is properly configured

6. **"Browser doesn't open for authentication"**
   - This is normal for the first time - the application will open your default browser
   - Complete the authentication in the browser and return to the application

### Getting Help

If you encounter issues:
1. Check the console output for detailed error messages
2. Verify all configuration steps were completed correctly
3. Ensure your Google account has the necessary permissions
4. Try deleting the authentication cache and re-authenticating
