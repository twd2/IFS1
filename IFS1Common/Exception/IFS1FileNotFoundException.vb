Public Class IFS1FileNotFoundException
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
    End Sub
End Class
