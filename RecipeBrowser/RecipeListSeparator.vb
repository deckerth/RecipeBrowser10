Public Class RecipeListSeparator
    Inherits Recipe

    Public Sub New(aHeaderText As String)
        ItemType = ItemTypes.Header
        RecipeContentVisibility = Visibility.Collapsed
        HeaderContentVisibility = Visibility.Visible
        Name = aHeaderText
    End Sub

End Class
