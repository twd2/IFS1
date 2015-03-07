Imports System.Runtime.InteropServices


<StructLayout(LayoutKind.Sequential, Pack:=1)>
Public Class MBR

    <MarshalAs(UnmanagedType.ByValArray, SizeConst:=446)>
    Public BootCode(446 - 1) As Byte

    Public Partition0, Partition1, Partition2, Partition3 As Partition

    <MarshalAs(UnmanagedType.ByValArray, SizeConst:=2)>
    Public Ending(1) As Byte

    Public Sub New()
        Ending(0) = &H55
        Ending(0) = &HAA
    End Sub
End Class
