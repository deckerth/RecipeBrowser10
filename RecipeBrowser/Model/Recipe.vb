Imports Windows.Storage
Imports Windows.Storage.Provider
Imports Windows.Globalization.DateTimeFormatting
Imports System.Globalization
Imports System.Text
Imports System.Xml.Serialization

Public Class Recipe
    Implements INotifyPropertyChanged

    Public Event PropertyChanged(ByVal sender As Object, ByVal e As PropertyChangedEventArgs) Implements INotifyPropertyChanged.PropertyChanged

    Protected Overridable Sub OnPropertyChanged(ByVal PropertyName As String)
        ' Raise the event, and make this procedure
        ' overridable, should someone want to inherit from
        ' this class and override this behavior:
        RaiseEvent PropertyChanged(Me, New PropertyChangedEventArgs(PropertyName))
    End Sub

    Public Property Categegory As String
    Public Property Name As String

    Private _SubTitle As String

    Public Property SubTitle As String
        Get
            Return _SubTitle
        End Get
        Set(value As String)
            If value <> _SubTitle Then
                _SubTitle = value
                OnPropertyChanged("SubTitle")
            End If
        End Set
    End Property

    Public Property CreationDateTime As DateTime
    Public Property NoOfPages As Integer
    Public Property CurrentPage As Integer
    Public Property RenderedPageNumber As Integer
    Public Property LastCooked As String
    Public Property CookedNoOfTimes As Integer

    Public Property File As Windows.Storage.StorageFile
    Public ReadOnly Property RenderedPage As BitmapImage
        Get
            Return _RenderedPage
        End Get
    End Property

    Public Property Notes As Windows.Storage.StorageFile

    Private _RenderedPage As BitmapImage
    Private _RenderedPages As New List(Of BitmapImage)
    Private _Document As Windows.Data.Pdf.PdfDocument

    Private _PageRendererRunning As Boolean

    Public Sub RenderSubTitle()

        'Dim stats = Statistics.data.GetStatistics(Categegory, Name)
        'SubTitle = Categegory + ", " + DateTimeFormatter.ShortDate.Format(CreationDateTime)
        'If stats IsNot Nothing Then
        '    SubTitle = SubTitle + ", " + App.Texts.GetString("CookedOn") + " " + stats.LastCooked
        '    SubTitle = SubTitle + " (" + App.Texts.GetString("UpToNow") + " " + stats.CookedNoOfTimes.ToString + " " + App.Texts.GetString("Times") + ")"
        'End If

        SubTitle = Categegory + ", " + DateTimeFormatter.ShortDate.Format(CreationDateTime)
        If CookedNoOfTimes > 0 Then
            SubTitle = SubTitle + ", " + App.Texts.GetString("CookedOn") + " " + LastCooked
            SubTitle = SubTitle + " (" + App.Texts.GetString("UpToNow") + " " + CookedNoOfTimes.ToString + " " + App.Texts.GetString("Times") + ")"
        End If

    End Sub

    Private Shared SeparatorString As String = "§§§§§§§"

    Public Function GetKey(folder As String) As String

        Return folder + SeparatorString + Categegory + SeparatorString + Name

    End Function

    Public Shared Sub GetCategoryAndNameFromKey(ByRef key As String, ByRef folder As String, ByRef category As String, ByRef name As String)

        Dim pos As Integer
        Dim tail As String

        folder = ""
        category = ""
        name = ""

        pos = key.IndexOf(SeparatorString)

        If pos = -1 Then
            Return
        End If

        folder = key.Substring(0, pos)
        tail = key.Substring(pos + SeparatorString.Count)

        pos = tail.IndexOf(SeparatorString)

        If pos = -1 Then
            Return
        End If

        category = tail.Substring(0, pos)
        name = tail.Substring(pos + SeparatorString.Count)

    End Sub

    Private Async Function WriteNotesToFileAsync(ByVal noteText As Windows.UI.Text.ITextDocument, ByVal file As Windows.Storage.StorageFile) As Task
        Try

            ' Prevent updates to the remote version of the file until we 
            ' finish making changes and call CompleteUpdatesAsync.
            CachedFileManager.DeferUpdates(file)
            ' write to file
            Dim randAccStream As Windows.Storage.Streams.IRandomAccessStream = Await file.OpenAsync(Windows.Storage.FileAccessMode.ReadWrite)

            noteText.SaveToStream(Windows.UI.Text.TextGetOptions.FormatRtf, randAccStream)

            randAccStream.Dispose()

            ' Let Windows know that we're finished changing the file so the 
            ' other app can update the remote version of the file.
            Dim status As FileUpdateStatus = Await CachedFileManager.CompleteUpdatesAsync(file)
            If (status <> FileUpdateStatus.Complete) Then
                Dim errorBox As Windows.UI.Popups.MessageDialog = New Windows.UI.Popups.MessageDialog(App.Texts.GetString("UnableToSaveNotes"))
                Await errorBox.ShowAsync()
            End If
        Catch ex As Exception
        End Try

    End Function

    Public Async Function UpdateNoteTextAsync(noteText As Windows.UI.Text.ITextDocument) As Task

        Dim allFolders = DirectCast(App.Current.Resources("recipeFolders"), RecipeFolders)

        If Notes Is Nothing Then
            Dim recipeFolder = allFolders.GetFolder(Categegory)
            Try
                Notes = Await recipeFolder.Folder.CreateFileAsync(Name + ".rtf")
            Catch ex As Exception
            End Try
        End If

        If Notes IsNot Nothing Then
            Await WriteNotesToFileAsync(noteText, Notes)
            allFolders.UpdateNote(Me)
        End If

    End Function

    Public Async Function LogRecipeCookedAsync(CookedOn As DateTimeOffset) As Task

        Dim allFolders = DirectCast(App.Current.Resources("recipeFolders"), RecipeFolders)

        LastCooked = DateTimeFormatter.ShortDate.Format(CookedOn)
        CookedNoOfTimes = CookedNoOfTimes + 1

        Await allFolders.UpdateStatisticsAsync(Me)
    End Function

    Function CookedToday() As Boolean

        Return CookedOn(Date.Now)

    End Function

    Function CookedOn(OnDate As DateTimeOffset) As Boolean

        Return LastCooked IsNot Nothing AndAlso LastCooked.Equals(DateTimeFormatter.ShortDate.Format(OnDate))

    End Function


    Public Async Function RenderPageAsync() As Task

        If _Document Is Nothing Or RenderedPageNumber = CurrentPage - 1 Or _PageRendererRunning Then
            Return
        End If

        RenderedPageNumber = CurrentPage - 1

        If _RenderedPages.Count >= CurrentPage Then
            _RenderedPage = _RenderedPages.Item(RenderedPageNumber)
            Return
        End If

        _PageRendererRunning = True

        Dim errorOccured As Boolean = False
        Dim permissionDenied As Boolean = False

        Try
            Dim page = _Document.GetPage(RenderedPageNumber)

            Await page.PreparePageAsync()

            Dim filename = Guid.NewGuid().ToString() + ".png"
            Dim tempFolder = Windows.Storage.ApplicationData.Current.LocalFolder
            Dim tempFile As Windows.Storage.StorageFile = Await tempFolder.CreateFileAsync(filename, Windows.Storage.CreationCollisionOption.ReplaceExisting)
            Dim tempStream = Await tempFile.OpenAsync(Windows.Storage.FileAccessMode.ReadWrite)

            Await page.RenderToStreamAsync(tempStream)
            Await tempStream.FlushAsync()
            tempStream.Dispose()
            page.Dispose()

            Dim renderedPicture = Await Windows.Storage.ApplicationData.Current.LocalFolder.GetFileAsync(filename)

            If renderedPicture IsNot Nothing Then

                ' Open a stream for the selected file.
                Dim fileStream = Await renderedPicture.OpenAsync(Windows.Storage.FileAccessMode.Read)

                ' Set the image source to the selected bitmap.
                _RenderedPage = New Windows.UI.Xaml.Media.Imaging.BitmapImage()

                Await RenderedPage.SetSourceAsync(fileStream)

                _RenderedPages.Add(_RenderedPage)
            End If
        Catch ex1 As System.UnauthorizedAccessException
            permissionDenied = True
            Exit Try
        Catch
            errorOccured = True
            Exit Try
        End Try

        _PageRendererRunning = False

        'If errorOccured Then
        '    Dim popup = New Windows.UI.Popups.MessageDialog("Das Rezept konnte nicht geladen werden.")
        '    Await popup.ShowAsync()
        '    Return Nothing
        'ElseIf permissionDenied Then
        '    Dim popup = New Windows.UI.Popups.MessageDialog("Der Zugriff auf das Rezept wurde verweigert.")
        '    Await popup.ShowAsync()
        '    Return Nothing
        'End If

    End Function

    Public Async Function LoadRecipeAsync() As Task

        If RenderedPage IsNot Nothing Then
            Return
        End If
        Try
            _Document = Await Windows.Data.Pdf.PdfDocument.LoadFromFileAsync(File)
            NoOfPages = _Document.PageCount
            CurrentPage = 1
            RenderedPageNumber = -1 ' Force read
            Await RenderPageAsync()
        Catch ex As Exception
            NoOfPages = 0
            CurrentPage = 0
        End Try

    End Function


    Public Async Function PreviousPage() As Task

        If CurrentPage > 1 Then
            CurrentPage = CurrentPage - 1
            Await RenderPageAsync()
        End If

    End Function

    Public Async Function NextPage() As Task

        If CurrentPage < NoOfPages Then
            CurrentPage = CurrentPage + 1
            Await RenderPageAsync()
        End If

    End Function

End Class

Public Class RecipeComparer_NameAscending
    Implements IComparer(Of Recipe)

    Public Function Compare(ByVal x As Recipe, ByVal y As Recipe) As Integer Implements IComparer(Of Recipe).Compare

        If x Is Nothing Then
            If y Is Nothing Then
                ' If x is Nothing and y is Nothing, they're
                ' equal. 
                Return 0
            Else
                ' If x is Nothing and y is not Nothing, y
                ' is greater. 
                Return -1
            End If
        Else
            ' If x is not Nothing...
            '
            If y Is Nothing Then
                ' ...and y is Nothing, x is greater.
                Return 1
            Else
                ' ...and y is not Nothing, compare the string
                Return x.Name.CompareTo(y.Name)
            End If
        End If
    End Function
End Class

Public Class RecipeComparer_DateDescending
    Implements IComparer(Of Recipe)

    Public Function Compare(ByVal x As Recipe, ByVal y As Recipe) As Integer Implements IComparer(Of Recipe).Compare

        If x Is Nothing Then
            If y Is Nothing Then
                ' If x is Nothing and y is Nothing, they're
                ' equal. 
                Return 0
            Else
                ' If x is Nothing and y is not Nothing, y
                ' is greater. 
                Return -1
            End If
        Else
            ' If x is not Nothing...
            '
            If y Is Nothing Then
                ' ...and y is Nothing, x is greater.
                Return 1
            Else
                ' ...and y is not Nothing, compare the string
                Return -1 * x.CreationDateTime.CompareTo(y.CreationDateTime)
            End If
        End If
    End Function
End Class


Public Class RecipeComparer_LastCookedDescending
    Implements IComparer(Of Recipe)

    Private Function ConvertToDate(ByRef datestr As String) As DateTime

        Try
            ' Create two different encodings.
            Dim ascii As Encoding = Encoding.GetEncoding("US-ASCII")
            Dim unicode As Encoding = Encoding.Unicode

            ' Convert the string into a byte array.
            Dim unicodeBytes As Byte() = unicode.GetBytes(datestr)

            ' Perform the conversion from one encoding to the other.
            Dim asciiBytes As Byte() = Encoding.Convert(unicode, ascii, unicodeBytes)

            ' Convert the new byte array into a char array and then into a string.
            Dim asciiChars(ascii.GetCharCount(asciiBytes, 0, asciiBytes.Length) - 1) As Char
            ascii.GetChars(asciiBytes, 0, asciiBytes.Length, asciiChars, 0)
            Dim asciiString As New String(asciiChars)

            Return DateTime.Parse(asciiString.Replace("?", ""))
        Catch ex As Exception
            Return Nothing
        End Try

    End Function

    Public Function Compare(ByVal x As Recipe, ByVal y As Recipe) As Integer Implements IComparer(Of Recipe).Compare

        If x Is Nothing OrElse x.LastCooked Is Nothing Then
            If y Is Nothing OrElse y.LastCooked Is Nothing Then
                ' If x is Nothing and y is Nothing, they're
                ' equal. 
                Return 0
            Else
                ' If x is Nothing and y is not Nothing, y
                ' is smaller. 
                Return 1
            End If
        Else
            ' If x is not Nothing...
            '
            If y Is Nothing OrElse y.LastCooked Is Nothing Then
                ' ...and y is Nothing, x is smaller.
                Return -1
            Else
                ' ...and y is not Nothing, compare the dates
                Dim xDate As DateTime
                Dim yDate As DateTime

                xDate = ConvertToDate(x.LastCooked)
                yDate = ConvertToDate(y.LastCooked)
                Return -1 * xDate.CompareTo(yDate)

            End If
        End If
    End Function
End Class
