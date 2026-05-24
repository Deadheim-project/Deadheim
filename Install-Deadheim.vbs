Set shell = CreateObject("WScript.Shell")
Set fso = CreateObject("Scripting.FileSystemObject")

scriptDir = fso.GetParentFolderName(WScript.ScriptFullName)
guiScript = fso.BuildPath(scriptDir, "Install-Deadheim-GUI.ps1")

If Not fso.FileExists(guiScript) Then
    MsgBox "Install-Deadheim-GUI.ps1 nao foi encontrado na mesma pasta.", vbCritical, "Deadheim Installer"
    WScript.Quit 1
End If

command = "powershell.exe -NoProfile -ExecutionPolicy Bypass -WindowStyle Hidden -File " & Chr(34) & guiScript & Chr(34)
shell.Run command, 0, False
