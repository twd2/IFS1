Public Class StartupArgsParser

    Public shortName As New Dictionary(Of Char, String)
    Public longName As New Dictionary(Of String, String)
    Public argObjs As New Dictionary(Of String, StartupArgsInfo)

    Public Sub AddArgument(name As String, sname As String, lname As String, minpc As Integer, maxpc As Integer)
        If sname = "" AndAlso lname = "" Then
            Throw New ArgumentNullException("sname, lname")
        End If
        If sname <> "" Then
            shortName.Add(sname.ToLower()(0), name)
        End If
        If lname <> "" Then
            longName.Add(lname.ToLower(), name)
        End If
        argObjs.Add(name, New StartupArgsInfo(minpc, maxpc))
    End Sub

    Public Function Parse(args As String()) As Dictionary(Of String, StartupArgsInfo)
        If args.Length = 0 Then
            Throw New StartupArgsParseException("No argument")
        End If
        Dim i = 0
        Do While i < args.Length
            Dim arg = args(i)
            If arg.StartsWith("--") Then
                Dim lname = arg.Substring(2).ToLower()
                If Not longName.ContainsKey(lname) Then
                    'Throw New StartupArgsParseException("Unknown argument """ + lname + """")
                    i += 1
                    Continue Do
                End If

                Dim obj = argObjs(longName(lname))
                obj.found = True

                If i + obj.minParamsCount >= args.Length Then
                    Throw New StartupArgsParseException("Bad argument """ & arg & """")
                End If

                Dim readCount = 0
                i += 1
                Do While readCount < obj.maxParamsCount AndAlso (i < args.Length)
                    obj.params.Add(args(i))
                    readCount += 1
                    i += 1
                Loop
                i -= 1
            ElseIf arg.StartsWith("-") Then
                For j = 1 To arg.Length - 1
                    Dim sname = arg.ToLower()(j)
                    If Not shortName.ContainsKey(sname) Then
                        'Throw New StartupArgsParseException("Unknown argument """ + sname + """")
                        Continue For
                    End If

                    Dim obj = argObjs(shortName(sname))
                    obj.found = True

                    If i + obj.minParamsCount >= args.Length Then
                        Throw New StartupArgsParseException("Bad argument """ & arg & """")
                    End If

                    Dim readCount = 0
                    i += 1
                    Do While readCount < obj.maxParamsCount AndAlso (i < args.Length)
                        obj.params.Add(args(i))
                        readCount += 1
                        i += 1
                    Loop
                    i -= 1
                Next
            Else
                'Throw New StartupArgsParseException("Bad argument """ & arg & """")
            End If

            i += 1
        Loop
        Return argObjs
    End Function

End Class
