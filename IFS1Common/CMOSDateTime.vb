Imports System.IO

Public Class CMOSDateTime

    'uint16	year;
    'uint8	month;
    'uint8	day;
    'uint8	day_of_week;
    'uint8	hour;
    'uint8	minute;
    'uint8	second;

    Public year As UInt16
    Public month As Byte
    Public day As Byte
    Public day_of_week As Byte
    Public hour As Byte
    Public minute As Byte
    Public second As Byte

    Public Shared dayofweekString = {
       "Sun", "Mon", "Tue", "Wed", "Thu", "Fri", "Sat"
    }

    ''' <summary>
    ''' 以当前时间创建对象
    ''' </summary>
    ''' <remarks></remarks>
    Public Sub New()
        SetDate(DateTime.Now)
    End Sub

    Public Sub New(d As DateTime)
        SetDate(d)
    End Sub

    Public Shared Function Read(s As Stream) As CMOSDateTime
        Dim r As New CMOSDateTime
        r.year = BinaryHelper.ReadInt16LE(s)
        r.month = s.ReadByte()
        r.day = s.ReadByte()
        r.day_of_week = s.ReadByte()
        r.hour = s.ReadByte()
        r.minute = s.ReadByte()
        r.second = s.ReadByte()
        Return r
    End Function

    Public Sub Write(s As Stream)
        BinaryHelper.WriteInt16LE(s, year)
        s.WriteByte(month)
        s.WriteByte(day)
        s.WriteByte(day_of_week)
        s.WriteByte(hour)
        s.WriteByte(minute)
        s.WriteByte(second)
    End Sub

    Public Overrides Function ToString() As String
        Return String.Format("{0}-{1}-{2} {3}:{4}:{5} {6}", year, month, day, hour, minute, second, dayofweekString(day_of_week))
    End Function

    Public Function ToDate() As DateTime
        Return New DateTime(year, month, day, hour, minute, second)
    End Function

    Public Sub SetDate(d As DateTime)
        year = d.Year
        month = d.Month
        day = d.Day
        day_of_week = d.DayOfWeek
        hour = d.Hour
        minute = d.Minute
        second = d.Second
    End Sub
End Class
