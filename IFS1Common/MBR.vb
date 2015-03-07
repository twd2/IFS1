Imports System.Runtime.InteropServices


<StructLayout(LayoutKind.Sequential, Pack:=1)>
Public Class MBR

    <MarshalAs(UnmanagedType.ByValArray, SizeConst:=446)>
    Public BootCode(446 - 1) As Byte

    Public Partition0 As New Partition, Partition1 As New Partition, Partition2 As New Partition, Partition3 As New Partition

    <MarshalAs(UnmanagedType.ByValArray, SizeConst:=2)>
    Public Ending(1) As Byte

    Public Sub New()
        Ending(0) = &H55
        Ending(0) = &HAA
    End Sub
End Class
