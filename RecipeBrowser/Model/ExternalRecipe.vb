Public Class ExternalRecipe
    Inherits Recipe

    Public Sub New(aCategory As String, aName As String)
        ItemType = ItemTypes.ExternalRecipe
        Category = aCategory
        Name = aName

        Dim db As Persistency.CookingHistory = Persistency.CookingHistory.Current
        If db.IsAvailable() Then
            db.SelectRecipe(aCategory, aName)
            CookedNoOfTimes = db.Selection.Count
            If CookedNoOfTimes > 0 Then
                ExternalSource = db.Selection(0).ExternalSource
                LastCooked = Recipe.ConvertToDateStr(Persistency.Helper.GetDate(db.Selection(0).LastCooked))
            End If
        End If
        RenderSubTitle()
    End Sub

    Public Sub New(aCategory As String, aName As String, aLastCookedDate As Date, aSource As String)
        ItemType = ItemTypes.ExternalRecipe
        Category = aCategory
        Name = aName
        CookedNoOfTimes = 1
        ExternalSource = aSource
        LastCooked = Recipe.ConvertToDateStr(aLastCookedDate)
        RenderSubTitle()
    End Sub

End Class
