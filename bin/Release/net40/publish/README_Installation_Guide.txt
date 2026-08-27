========================================================================
                     MERO DOKAN - INSTALLATION GUIDE
========================================================================

Welcome to Mero Dokan, the premium Shop Management System. Follow the
steps below to set up and run this application on a new computer.

------------------------------------------------------------------------
1. SYSTEM REQUIREMENTS
------------------------------------------------------------------------
- Operating System: Windows 7 / 8 / 10 / 11
- Framework: .NET Framework 4.0 or higher
- Database: Microsoft SQL Server (LocalDB or Express Edition)
  * Recommendation: Install SQL Server LocalDB 2019 or 2022.

------------------------------------------------------------------------
2. DATABASE SETUP (FIRST RUN)
------------------------------------------------------------------------
Mero Dokan is designed to automatically set up its own database on
first run.

1. Ensure SQL Server (LocalDB) or SQL Server Express is installed and
   running on the target computer.
2. Run MeroDokan.exe.
3. The application will automatically probe for available local SQL
   Server instances, create the database 'MeroDokanDB', and set up the
   required tables and indexes automatically.
4. If a custom database connection is required (e.g., if you are using
   a network server or SQL Server Express under a different name):
   - Click the "Configure Database Connection" link at the bottom of the
     Login screen.
   - Enter your SQL Server name and authentication credentials, test the
     connection, and click Save.

------------------------------------------------------------------------
3. DEFAULT LOGIN CREDENTIALS
------------------------------------------------------------------------
On database initialization, a default administrator account is seeded
automatically:

- Username: admin
- Password: admin

* IMPORTANT: Please change this default password inside the "Profile
  Settings" tab immediately after logging in for security.

------------------------------------------------------------------------
4. PACKAGE CONTENTS
------------------------------------------------------------------------
- MeroDokan.exe           - Main application executable.
- MeroDokan.exe.config    - System configuration file.
- QRCoder.dll             - Helper library for generating receipt QR codes.
- license.lic             - License file.
- Assets/                 - Subdirectory containing local image assets.
========================================================================
