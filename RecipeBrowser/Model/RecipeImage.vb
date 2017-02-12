Imports Windows.Storage

Public Class RecipeImage
    Implements INotifyPropertyChanged

    Public Event PropertyChanged(ByVal sender As Object, ByVal e As PropertyChangedEventArgs) Implements INotifyPropertyChanged.PropertyChanged

    Protected Overridable Sub OnPropertyChanged(ByVal PropertyName As String)
        ' Raise the event, and make this procedure
        ' overridable, should someone want to inherit from
        ' this class and override this behavior:
        RaiseEvent PropertyChanged(Me, New PropertyChangedEventArgs(PropertyName))
    End Sub

    Private _file As StorageFile
    Public Property File As StorageFile
        Get
            Return _file
        End Get
        Set(value As StorageFile)
            _file = value
            OnPropertyChanged("File")
        End Set
    End Property

    Private _image As BitmapImage
    Public Property Image As BitmapImage
        Get
            Return _image
        End Get
        Set(value As BitmapImage)
            _image = value
            OnPropertyChanged("Image")
        End Set
    End Property

    Public Sub New(aFile As StorageFile, anImage As BitmapImage)
        File = aFile
        Image = anImage
    End Sub
End Class
