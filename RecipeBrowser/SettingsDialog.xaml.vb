' Die Elementvorlage "Inhaltsdialog" ist unter http://go.microsoft.com/fwlink/?LinkId=234238 dokumentiert.

Public NotInheritable Class SettingsDialog
    Inherits ContentDialog

    Public Sub New()

        ' Dieser Aufruf ist für den Designer erforderlich.
        InitializeComponent()

        ' Fügen Sie Initialisierungen nach dem InitializeComponent()-Aufruf hinzu.
        LoggingEnabledSwitch.IsOn = App.Logger.IsEnabled
    End Sub

    Private Sub ContentDialog_PrimaryButtonClick(sender As ContentDialog, args As ContentDialogButtonClickEventArgs)
        Hide()
    End Sub

    Private Sub ChangeRootFolder_Click(sender As Object, e As RoutedEventArgs) Handles ChangeRootFolder.Click
        CategoryOverview.Current.ChangeRootFolderRequested = True
        Hide()
    End Sub

    Private Sub LoggingEnabledSwitch_Toggled(sender As Object, e As RoutedEventArgs) Handles LoggingEnabledSwitch.Toggled
        App.Logger.SetActive(LoggingEnabledSwitch.IsOn)
    End Sub
End Class
