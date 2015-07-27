Imports System.IO

Public Class LoggerWrapper
    Inherits TextWriter

    Private _sw As New Stopwatch
    Private _innerTW As TextWriter

    Public Sub New(tw As TextWriter)
        _innerTW = tw
        _sw.Start()
    End Sub

    Public Overrides Sub WriteLine()
        WriteLine("")
    End Sub

    Public Overrides Sub WriteLine(value As String)
        'MyBase.WriteLine(value)
        If _innerTW IsNot Nothing Then
            _innerTW.WriteLine("[{0:N7}] {1}", _sw.Elapsed.TotalSeconds, value)
        End If
    End Sub

    Public Overrides Sub WriteLine(format As String, ParamArray arg() As Object)
        WriteLine(String.Format(format, arg))
    End Sub

    Public Overrides Sub Write(value As String)
        If _innerTW IsNot Nothing Then
            _innerTW.Write(value)
        End If
    End Sub

    Public Overrides ReadOnly Property Encoding As Text.Encoding
        Get
            If _innerTW IsNot Nothing Then
                Return _innerTW.Encoding
            Else
                Return Text.Encoding.UTF8
            End If
        End Get
    End Property
End Class
