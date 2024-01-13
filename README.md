# DCIM Ingester
A simple system tray application that ingests images from SD cards and sorts them into folders by date taken. The user is automatically prompted to start an ingest whenever an applicable volume is mounted to the system.

- The user is only prompted to ingest if the volume contains a DCIM folder and contains at least one ingestable file.
- Within DCIM, only folders that conform to the DCF specification are ingested from, which means non-image directories that cameras often create are ignored.
- Files with a date taken EXIF attribute are sorted into folders by date taken (folder structure is configurable). All other files are ingested into an "Unsorted" folder.
- If a file being ingested already exists in the destination then its entire contents are compared to determine whether it is a duplicate or not.
- You may append your own text (following a space) to the deepest level folder name that DCIM Ingester automatically creates during ingest and it will still use that folder in future ingests if necessary. For example: `C:/Images/2023/12/25 -- Christmas Day`

# Usage
- Open and run the project in Visual Studio (requires .NET 8).
- Set the Windows Autoplay action for memory cards to "no action" to prevent Autoplay from showing its own notification in front of the DCIM Ingester window when you insert an SD card.
- By default, files will be ingested into a directory structure starting in the user's Pictures folder. This can be changed in the Settings window, which is accessed by right-clicking on the SD card icon in the system tray.
	- The directory structure that DCIM Ingester creates can also be changed.
- Insert an SD card (or any removable drive containing a valid DCIM folder) and respond to the prompt.

# Version History
- 1.0 (Dec 21, 2019) -- First version
- 1.1 (Jan 16, 2020)
	- Fixed an issue where the ingest progress did not update after the final file was ingested
	- Added checks for existence of the destination directory
	- Added a selectable list of destination directory structure formats
	- Can now ingest all file names instead of only those covered by the DCF specification
- 2.0 (Feb 15, 2021)
	- Major rework of the code including an upgrade to .NET 5
	- A few small UI changes
- 2.1 (Apr 28, 2021)
	- Significant change to the way volumes are detected
	- Settings now opens at startup if the ingest destination is not set
	- Drive labels are now included next to drive letters
	- Fixed an issue where the ingest percentage would not display until the first file had been ingested
	- Updated the SD card directory name regex to better comply with the DCF specification
	- Various other assorted improvements
	- Added some more code documentation
- 2.2 (Dec 25, 2021)
	- Fixed an issue where the "Open Folder" button opens the source folder instead of the destination folder
- 2.3 (Sep 11, 2022)
	- Reimplemented volume detection using SHChangeNotifyRegister. This fixes an issue where an SD card inserted into a connected card reader would not prompt
	- Fixed an issue where cancelling the Settings window without having an ingest destination set (e.g. on first run) would throw an exception
	- Ingests can now happen if Settings is open, and Settings can now be opened if an ingest is in progress
	- The window now automatically repositions if display settings are changed (resolution, display removal, etc.)
	- Restricted Settings from being able to be opened multiple times simultaneously
	- Various other small improvements
- 3.0 (Jan 13, 2024)
	- Redesigned UI to match that of Windows 11 notifications
	- Added file contents comparison to determine whether a file that already exists in the destination is a duplicate
	- Moved the option to delete originals after ingest to the Settings window
	- On first run, the destination directory defaults to the Pictures folder instead of being left unset
	- Removed custom UI styling from the Settings window
	- Changed from preventing application exit while ingests are in progress to allowing such with a confirmation dialog
	- Upgraded to .NET 8#