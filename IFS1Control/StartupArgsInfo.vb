Public Class StartupArgsInfo

    Public name As String
    Public found As Boolean
    Public maxParamsCount As Integer
    Public minParamsCount As Integer
    Public params As New List(Of String)

    Public Sub New(minParamsCount As Integer, maxParamsCount As Integer)
        Me.maxParamsCount = maxParamsCount
        params.Capacity = maxParamsCount
        Me.minParamsCount = minParamsCount
    End Sub


End Class
