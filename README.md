# SharePointAdminTool

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

Technologies used:
C# (.NET Windows Form)
Microsoft Graph SDK
Webview 2 Runtime Engine: Sign In Functionality

## Outcome
Increased security and consistency for enterprise management of SharePoint across all unit site collections

## Further insight:
The program uses the following Entra API roles to function:
1. User.read.all (delegated through app client id), to read user metadata such as jobTitle, Department, and mail nickname
2. Group.readwrite.all (delegated through app client id), to read/write to existing entra security groups
3. AdministrativeUnit.ReadWrite.All (acquired through JIT), used to create security groups inside the designated administrative unit

As the third API role is only permitted to be acquired through JIT elevation, it was only possible to integrate it into the existing app by using first party consented Powershell Graph SDK alongside Powershell 7.
Powershell 7 is required because of its support for modern authentication using MSAL, unlike its previous iteration.

An API call is then made iteratively to https://graph.microsoft.com/beta/directory/administrativeUnits/{administrativeUnitId}/members

The app initially acquires only the first two roles, you can sign in and paste the JWT into a tool like JWT.MS and see that the role claims only acknowledges the existence of these.
The app is strictly configured to manage security groups containing either CSG-CLBA or FSG-CLBA 

For an end-user that wishes to audit their departments SharePoint site, the program attempts to obfuscate other department security groups by means of using Principal Claims.
The JWT acquires an app role specified by a naming scheme of CLBA-DEPT from the app registration itself. The app role itself determines what department they can view.

Admins, on the other hand, acquire a role of ADMIN and can view all department groups.

Lastly, auditing was possible but only locally using a serialized JSON.
I opted for this because auditing technically is already entrenched into the Microsoft 365 ecosystem, and the goal is to move this to a Power App eventually. 

Any questions can be redirected to cnotzon98@tamu.edu




