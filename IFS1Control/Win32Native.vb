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

    Public Const FILE_BEGIN = 0
    Public Const FILE_CURRENT = 1
    Public Const FILE_END = 2

    <DllImport("kernel32")>
    Public Shared Function SetFilePointerEx(ByVal hFile As Integer, lDistanceToMove As Long, ByRef lpNewFilePointer As Long, dwMoveMethod As Integer) As Integer

    End Function

    <DllImport("kernel32")>
    Public Shared Function GetFileSizeEx(ByVal hFile As Integer, ByRef lpFileSizeHigh As Long) As Integer

    End Function

    <DllImport("kernel32")>
    Public Shared Function GetDiskFreeSpaceEx(ByVal lpDirectoryName As String, ByRef lpFreeBytesAvailable As ULong, ByRef lpTotalNumberOfBytes As ULong, ByRef lpTotalNumberOfFreeBytes As ULong) As Integer

    End Function

    <DllImport("kernel32")>
    Public Shared Function GetLogicalDriveStrings(ByVal nBufferLength As Integer, lpBuffer As IntPtr) As Integer

    End Function


    <DllImport("kernel32")>
    Public Shared Function GetVolumeInformation(ByVal lpRootPathName As String, lpVolumeNameBuffer As IntPtr, nVolumeNameSize As Integer,
                                                ByRef lpVolumeSerialNumber As Integer, ByRef lpMaximumComponentLength As Integer, ByRef lpFileSystemFlags As Integer, ByRef lpFileSystemNameBuffer As Integer,
                                                nFileSystemNameSize As Integer) As Integer

    End Function
    'BOOL WINAPI DeviceIoControl(
    '  _In_         HANDLE hDevice,
    '  _In_         DWORD dwIoControlCode,
    '  _In_opt_     LPVOID lpInBuffer,
    '  _In_         DWORD nInBufferSize,
    '  _Out_opt_    LPVOID lpOutBuffer,
    '  _In_         DWORD nOutBufferSize,
    '  _Out_opt_    LPDWORD lpBytesReturned,
    '  _Inout_opt_  LPOVERLAPPED lpOverlapped
    ');

    <DllImport("kernel32")>
    Public Shared Function DeviceIoControl(hDevice As Integer, dwIoControlCode As Integer,
                                           lpInBuffer As IntPtr, nInBufferSize As Integer,
                                           lpOutBuffer As IntPtr, nOutBufferSize As Integer,
                                           ByRef lpBytesReturned As Integer, lpOverlapped As IntPtr) As Integer

    End Function

    Public Const IOCTL_DISK_GET_LENGTH_INFO = 475228

    Public Shared Sub AssumeNoError()
        Dim errno = Win32Native.GetLastError()
        If errno = 0 Then
            Return
        End If
        Throw New Exception("Win32 GetLastError: " + errno.ToString())
    End Sub
End Class
