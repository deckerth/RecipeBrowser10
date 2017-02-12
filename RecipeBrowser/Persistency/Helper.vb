Namespace Global.Persistency

    Public Class Helper

        Public Shared Function GetSQLiteDate(aDate As Date) As String

            Dim year As String = aDate.Year.ToString
            Dim month As String = aDate.Month.ToString
            If month.Length = 1 Then
                month = "0" + month
            End If
            Dim day As String = aDate.Day.ToString
            If day.Length = 1 Then
                day = "0" + day
            End If
            Return year + "-" + month + "-" + day

        End Function


        Public Shared Function GetDate(aDate As String) As Date

            ' aDate has the form yyyy-mm-dd

            Dim result As Date
            If Date.TryParse(aDate, result) Then
                Return result
            Else
                Return Date.MaxValue
            End If

        End Function

    End Class

End Namespace
