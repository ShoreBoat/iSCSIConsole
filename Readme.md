# iSCSI Console AutoStart

This repository is a fork of [TalAloni/iSCSIConsole](https://github.com/TalAloni/iSCSIConsole) with Windows AutoStart and target persistence support added on top of iSCSI Console v1.5.6.1.

> Original project and iSCSI implementation by Tal Aloni. This fork keeps the original license and credits.

## AutoStart fork features

- Persists the selected IP address and TCP port.
- Persists iSCSI target IQNs and virtual disk image paths.
- Restores saved VHD / VMDK / IMG targets when the application starts.
- Automatically starts the iSCSI target server after restoring saved targets.
- Supports the `/autostart` launch argument and minimizes the window when launched automatically.
- Registers a Windows scheduled task so the target server can start automatically after Windows sign-in.
- Targets .NET Framework 4.7.2 for the Windows build.

## Windows 11 quick start

1. Download the complete ZIP from the GitHub Release and extract all files to a permanent folder. Do not run `ISCSIConsole.exe` by itself without the DLLs and `ISCSIConsole.exe.config` next to it.
2. Run `ISCSIConsole.exe` as Administrator.
3. Select the LAN IP address to listen on, or use `Any` to listen on all interfaces. Keep TCP port `3260` unless you have a reason to change it.
4. Click **Add Target** and keep or enter an IQN, for example `iqn.1991-05.com.microsoft:target1`.
5. Add an existing virtual disk such as `D:\ISCSI\GameDisk.vhd`, or create a new virtual disk.
6. Click **Start**. The current server and target configuration is saved for AutoStart.
7. On the client PC, open the Windows iSCSI Initiator (`iscsicpl`), discover the server PC's LAN IP, connect to the target, then bring the disk online / assign a drive letter in Disk Management if required.

After configuration has been saved, signing back into Windows on the server PC should automatically launch iSCSI Console, restore the target, load the virtual disk and start listening on TCP 3260.

## Game storage notes

The iSCSI disk is presented to the client as block storage, so Windows and game launchers generally treat it like a locally attached disk. Steam and other game libraries can be placed on it after the disk is initialized and formatted on the client.

For game usage, wired Gigabit Ethernet or faster is strongly recommended. Only one machine should mount a normal NTFS volume read/write at a time unless the filesystem and application are explicitly designed for shared block storage.

## Configuration / AutoStart notes

- Keep the extracted application folder at a stable path after AutoStart has been configured, because the Windows scheduled task launches that executable path.
- Keep the configured VHD/VMDK/IMG file at the same path on the server PC.
- If Windows Firewall blocks TCP 3260, create an inbound rule for the application or TCP port 3260 on the private LAN profile.
- If the server PC changes LAN IP, update the iSCSI Initiator discovery address on the client or use a DHCP reservation / static LAN address.

## About iSCSI Console

iSCSI Console is a Free, Open Source, User-Mode iSCSI Target Server written in C#.  
iSCSI Console can serve physical and virtual disks to multiple clients.

## About the iSCSI library

The iSCSI library utilized by iSCSI Console was designed to give developers an easy way to serve block storage via iSCSI.  
Any storage object you wish to share needs to implement the abstract Disk class, and the library will take care of the rest.  
The library was written with extensibility in mind, and was designed to fit multitude of projects.  
iSCSI Console is merely a demo project that exposes some of the capabilities of this library.

A NuGet package of the original library [is available](https://www.nuget.org/packages/ISCSI/).

## What this program can do

1. Serve virtual disks (VHD / VMDK / IMG).
2. Serve physical disks.
3. Serve basic volumes as disks.
4. Serve dynamic volumes as disks.
5. Create VHDs.
6. Restore saved virtual-disk targets automatically on Windows in this fork.
7. Automatically start the target server after Windows sign-in in this fork.

![iSCSI Console UI](ISCSIConsole_UI.png)

## Original author

Tal Aloni <tal.aloni.il@gmail.com>
