Imports IFS1Common

Module Module1

    Sub Main()
        Dim a = BinaryHelper.ToArray(Of Int32)({1, 0, 0, 0, 5, 0, 0, 0})
        Dim b = BinaryHelper.ToBytes(Of Int32)({1, 123456, 98765432})
    End Sub

End Module
