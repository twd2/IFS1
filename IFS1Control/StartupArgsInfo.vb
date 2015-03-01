Public Class StartupArgsInfo

    Public name As String
    Public found As Boolean
    Public maxParamsCount As Integer
    Public minParamsCount As Integer
    Public params As New List(Of String)

    Public Sub New(minpc As Integer, maxpc As Integer)
        maxParamsCount = maxpc
        params.Capacity = maxpc
        minParamsCount = minpc
    End Sub


End Class
