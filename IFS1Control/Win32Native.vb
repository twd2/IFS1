Imports System.Runtime.InteropServices

Public Class Win32Native

    Public Const GENERIC_ALL = &H10000000
    Public Const GENERIC_READ As Integer = &H80000000
    Public Const GENERIC_WRITE As Integer = &H40000000
    Public Const OPEN_EXISTING = 3

    <DllImport("kernel32")>
    Public Shared Function CloseHandle(ByVal hObject As Integer) As Integer

    End Function

    <DllImport("kernel32")>
    Public Shared Function CreateFile(ByVal lpFileName As String, ByVal dwDesiredAccess As Integer, ByVal dwShareMode As Integer, ByVal lpSecurityAttributes As Integer, ByVal dwCreationDisposition As Integer, ByVal dwFlagsAndAttributes As Integer, ByVal hTemplateFile As Integer) As Integer

    End Function

    <DllImport("kernel32")>
    Public Shared Function GetLastError() As Integer

    End Function

    <DllImport("kernel32")>
    Public Shared Function SetLastError(errno As Integer) As Integer

    End Function

    <DllImport("kernel32")>
    Public Shared Function ReadFile(ByVal hFile As Integer, ByRef lpBuffer As Byte, ByVal nNumberOfBytesToRead As Integer, ByRef lpNumberOfBytesRead As Integer, ByVal lpOverlapped As Integer) As Integer

    End Function

    <DllImport("kernel32")>
    Public Shared Function WriteFile(ByVal hFile As Integer, ByRef lpBuffer As Byte, ByVal nNumberOfBytesToWrite As Integer, ByRef lpNumberOfBytesWritten As Integer, ByVal lpOverlapped As Integer) As Integer 'OVERLAPPED

    End Function

    <DllImport("kernel32")>
    Public Shared Function SetFilePointerEx(ByVal hFile As Integer, lDistanceToMove As Long, ByRef lpDistanceToMoveHigh As Integer, dwMoveMethod As Integer) As Integer

    End Function

    <DllImport("kernel32")>
    Public Shared Function GetFileSize(ByVal hFile As Integer, ByRef lpFileSizeHigh As Integer) As Integer

    End Function

    Public Shared Sub AssumeNoError()
        Dim errno = Win32Native.GetLastError()
        If errno = 0 Then
            Return
        End If
        Throw New Exception("Win32 GetLastError: " + errno.ToString())
    End Sub
End Class
