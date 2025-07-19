# TAMU-CLBA SharePoint Admin Tool

## Synopsis
This is a Windows-based application written in C# to streamline and support management of SharePoint Online specific security groups that are tied to Microsoft Entra ID. 

This tool was created as part of a full-scale digital transformation initiative, in which I led the architecture and execution of a complete migration to Microsoft 365 cloud services for my unit.

## Purpose:
To allow seamless enterprise management between the unit embedded IT admins and end-users.

The program automates tasks such as:
1. Mirroring security groups from one user to another.
2. Bulk actions such as adding/removing ownership to groups directly
3. Auditing of particular groups with the possibility to export as CSV or JSON.
4. Simplified security group creation using the Powershell Graph SDK

## Technologies used:

1. C# (.NET Windows Form)
2. Microsoft Graph SDK
3. Webview 2 Runtime Engine for Sign-In Functinality

## Outcome
Increased security and consistency for enterprise management of SharePoint across all unit site collections

## Further insight:
The program uses the following Entra API roles to function:
1. User.Read.All (delegated through app client id), to read user metadata such as jobTitle, Department, and mail nickname
2. GroupMember.ReadWrite.All (delegated through app client id), to read/write to existing entra security groups
3. AdministrativeUnit.ReadWrite.All (delegated through JIT), used to create security groups inside the designated administrative unit

As the third API role is only permitted to be acquired through JIT elevation in this environment, it was only possible to integrate it into this existing app by using first party consented Powershell Graph SDK alongside Powershell 7.
The groupsearchform class instantiates a method in which the relevant powershell script is called with pwsh, and then prompts for input accordingly.

If you have the right to acquire consent through an app registration for the third role, you should build your own class that contains this functionality.

## API calls used 

1. https://graph.microsoft.com/v1.0/directoryObjects/{id}
2. https://graph.microsoft.com/beta/directory/administrativeUnits/{administrativeUnitId}/members

## Final Note:

The app is strictly configured to manage security groups that 'startsWith' CSG-CLBA or FSG-CLBA 

For an end-user that wishes to audit their own department SharePoint site, the program attempts to obfuscate other department security groups by means of using Principal Claims.
The JWT acquires an app role specified by a naming scheme of CLBA_DEPT from the app registration itself. The app role itself determines what department they can view.

Admins, on the other hand, acquire a role of ADMIN and can view all department groups.

I did provide auditing but only locally using a serialized JSON.
I did not go through the effort of setting up a backend database management server to do this, because in essence auditing is already entrenched into Microsoft 365 itself, and the intended goal is to move this to the Microsoft Power Platform.

Any questions can be redirected to cnotzon98@tamu.edu




