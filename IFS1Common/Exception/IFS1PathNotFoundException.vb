Imports System.Text

Public Class IFS1PathNotFoundException
    Inherits IFS1Exception

    Public patharray As String()

    Public Sub New()
        MyBase.New()
    End Sub

    Public Sub New(msg As String)
        MyBase.New(msg)
    End Sub

    Public Sub New(patharray As String())
        MyBase.New()
        Me.patharray = patharray
        Dim sb As New StringBuilder()
        If patharray.Length > 0 Then
            For i = 0 To patharray.Length - 1
                sb.Append("/" + patharray(i))
            Next
        Else
            sb.Append("/")
        End If
        _msg = sb.ToString()
    End Sub

    Private _msg As String

    Public Overrides ReadOnly Property Message As String
        Get
            Return _msg
        End Get
    End Property

End Class
