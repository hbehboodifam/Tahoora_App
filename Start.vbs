Option Explicit

Dim WshShell, fso, scriptDir, apiPath, appPath
Dim ip, msg, ipConfig, ipLine, arr

Set WshShell = CreateObject("WScript.Shell")
Set fso = CreateObject("Scripting.FileSystemObject")

scriptDir = fso.GetParentFolderName(WScript.ScriptFullName)
apiPath = scriptDir & "\OrderManagementApi\OrderManagementApi"
appPath = scriptDir & "\OrderManagementApp\OrderManagementApp"

' Start API (hidden)
WshShell.CurrentDirectory = apiPath
WshShell.Run "cmd /c dotnet run > """ & scriptDir & "\api.log"" 2>&1", 0, False

WScript.Sleep 8000

' Start Blazor (hidden)
WshShell.CurrentDirectory = appPath
WshShell.Run "cmd /c dotnet run > """ & scriptDir & "\app.log"" 2>&1", 0, False

WScript.Sleep 12000

' Find local IPv4
ip = "localhost"
On Error Resume Next
Set ipConfig = WshShell.Exec("cmd /c ipconfig")
Do While Not ipConfig.StdOut.AtEndOfStream
    ipLine = ipConfig.StdOut.ReadLine()
    If InStr(ipLine, "IPv4") > 0 Then
        arr = Split(ipLine, ":")
        If UBound(arr) >= 1 Then
            ip = Trim(arr(1))
            Exit Do
        End If
    End If
Loop
On Error Goto 0

' Open browser
WshShell.Run "http://localhost:5255"

' Show info
msg = "Order Management System is ready!" & vbCrLf & vbCrLf & _
      "Local:   http://localhost:5255" & vbCrLf & _
      "Mobile:  http://" & ip & ":5255" & vbCrLf & vbCrLf & _
      "To stop: double-click Stop.bat"

MsgBox msg, 64, "Order Management"