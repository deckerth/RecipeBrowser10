Imports SQLite

Namespace Global.Persistency

    Public Class CookedRecipe
        <PrimaryKey()> <AutoIncrement()>
        Public Property Id As Integer

        Public Property Category As String
        Public Property Title As String
        Public Property LastCooked As String
        Public Property ExternalSource As String

        Public Sub New()
            Title = String.Empty
            LastCooked = String.Empty
            ExternalSource = String.Empty
            Category = String.Empty
        End Sub

        Public Sub New(theCategory As String, theTitle As String, cookedOnDate As Date, Optional source As String = "")

            Category = theCategory
            Title = theTitle
            LastCooked = Persistency.Helper.GetSQLiteDate(cookedOnDate)
            ExternalSource = source

        End Sub
    End Class

End Namespace
