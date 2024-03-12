# ValorantApp

## Initial Setup

1. **Create a Discord Bot Application:**
   - Go to [Discord Developer Portal](https://discord.com/developers/applications)
   - Create a new application for your bot.
   - Note down your bot's token and your Discord channel's ID. These will be needed in `App.config`.

2. **Add Bot to Discord Server:**
   - Add your bot to your Discord server.
   - Grant the following permissions to your bot (at least):
     - Slash Commands
     - Send Messages
     - Read Messages / View Channels

3. **Configuration:**
   - Update `App.config` with your bot's token and your Discord channel's ID.
   - To prevent Git from tracking changes to `App.config`, run:
     ```
     git update-index --assume-unchanged ValorantApp/App.config
     ```

4. **Development Environment Setup:**
   - Download and install Visual Studio.
   - Open `ValorantApp.sln` in Visual Studio.
   - Install all required NuGet dependencies.

5. **Database Setup:**
   - Download and install SQLite3 for your database.