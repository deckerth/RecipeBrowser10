Imports Windows.ApplicationModel.Background
Imports Windows.ApplicationModel.Resources
Imports Windows.UI.Core
''' <summary>
''' Stellt das anwendungsspezifische Verhalten bereit, um die Standardanwendungsklasse zu ergänzen.
''' </summary>
NotInheritable Class App
    Inherits Application

    Public Shared Texts As ResourceLoader = New Windows.ApplicationModel.Resources.ResourceLoader()
    Public Shared Logger As Logging = New Logging()
    Public Shared SearchBoxIsSupported As Boolean
    Public Shared SearchBoxVisibility As Visibility = Visibility.Collapsed
    Public Shared AutoSuggestBoxVisibility As Visibility = Visibility.Collapsed

    ''' <summary>
    ''' Wird aufgerufen, wenn die Anwendung durch den Endbenutzer normal gestartet wird. Weitere Einstiegspunkte
    ''' werden verwendet, wenn die Anwendung zum Öffnen einer bestimmten Datei, zum Anzeigen
    ''' von Suchergebnissen usw. gestartet wird.
    ''' </summary>
    ''' <param name="e">Details über Startanforderung und -prozess.</param>
    Protected Overrides Async Sub OnLaunched(e As Windows.ApplicationModel.Activation.LaunchActivatedEventArgs)
#If DEBUG Then
        ' Während des Debuggens Profilerstellungsinformationen zur Grafikleistung anzeigen.
        'If System.Diagnostics.Debugger.IsAttached Then
        '    ' Zähler für die aktuelle Bildrate anzeigen
        '    Me.DebugSettings.EnableFrameRateCounter = True
        'End If
#End If

        Dim rootFrame As Frame = TryCast(Window.Current.Content, Frame)

        ' App-Initialisierung nicht wiederholen, wenn das Fenster bereits Inhalte enthält.
        ' Nur sicherstellen, dass das Fenster aktiv ist.
        Dim categories = DirectCast(App.Current.Resources("recipeFolders"), RecipeFolders)

        AddHandler Windows.UI.Core.SystemNavigationManager.GetForCurrentView().BackRequested, AddressOf App_OnBackrequested

        Logger.Write("Launching. Previous activation state: " + e.PreviousExecutionState.ToString)

        If rootFrame Is Nothing Then
            ' Frame erstellen, der als Navigationskontext fungiert und zum Parameter der ersten Seite navigieren
            rootFrame = New Frame()

            RecipeBrowser.Common.SuspensionManager.RegisterFrame(rootFrame, "appFrame")

            ' Standardsprache festlegen
            rootFrame.Language = Windows.Globalization.ApplicationLanguages.Languages(0)

            AddHandler rootFrame.NavigationFailed, AddressOf OnNavigationFailed

            If e.PreviousExecutionState = ApplicationExecutionState.Terminated Then
                ' TODO: Zustand von zuvor angehaltener Anwendung laden
                Logger.Write("Restoring application state")
                If rootFrame.Content Is Nothing Then
                    Logger.Write("rootFrame.Content Is Nothing")
                End If
                Await RecipeBrowser.Common.SuspensionManager.RestoreAsync()
                If rootFrame.Content Is Nothing Then
                    Logger.Write("rootFrame.Content Is still Nothing")
                End If
            End If
            ' Den Frame im aktuellen Fenster platzieren
            Window.Current.Content = rootFrame
            End If

            Try
            Dim searchSuggestions = New Windows.ApplicationModel.Search.LocalContentSuggestionSettings()
            SearchBoxIsSupported = True
            SearchBoxVisibility = Visibility.Visible
        Catch ex As Exception
            AutoSuggestBoxVisibility = Visibility.Visible
        End Try

        If categories IsNot Nothing AndAlso Not categories.ContentLoaded Then
            Await categories.LoadAsync()
        End If

        If Timers.Factory.Current Is Nothing Then
            Logger.Write("Creating timers..")

            Dim factory = New Timers.Factory
            factory.Initialize()
            factory.CreateTimer()
            factory.CreateTimer()
            factory.CreateTimer()
            factory.CreateTimer()
        End If

        If History.Current IsNot Nothing Then
            Logger.Write("History state:" + History.Current.IsInitialized.ToString)
        End If

        If rootFrame.Content Is Nothing Then
            ' Wenn der Navigationsstapel nicht wiederhergestellt wird, zur ersten Seite navigieren
            ' und die neue Seite konfigurieren, indem die erforderlichen Informationen als Navigationsparameter
            ' übergeben werden
            Logger.Write("Navigating to main page")
            If categories IsNot Nothing AndAlso categories.ContentLoaded Then
                rootFrame.Navigate(GetType(MainPage), e.Arguments)
            Else
                rootFrame.Navigate(GetType(StartPage), e.Arguments)
            End If
        End If

        ' Sicherstellen, dass das aktuelle Fenster aktiv ist
        Window.Current.Activate()
    End Sub

    ''' <summary>
    ''' Wird aufgerufen, wenn die Navigation auf eine bestimmte Seite fehlschlägt
    ''' </summary>
    ''' <param name="sender">Der Rahmen, bei dem die Navigation fehlgeschlagen ist</param>
    ''' <param name="e">Details über den Navigationsfehler</param>
    Private Sub OnNavigationFailed(sender As Object, e As NavigationFailedEventArgs)
        Throw New Exception("Failed to load Page " + e.SourcePageType.FullName)
    End Sub

    ''' <summary>
    ''' Wird aufgerufen, wenn die Ausführung der Anwendung angehalten wird.  Der Anwendungszustand wird gespeichert,
    ''' ohne zu wissen, ob die Anwendung beendet oder fortgesetzt wird und die Speicherinhalte dabei
    ''' unbeschädigt bleiben.
    ''' </summary>
    ''' <param name="sender">Die Quelle der Anhalteanforderung.</param>
    ''' <param name="e">Details zur Anhalteanforderung.</param>
    Private Async Sub OnSuspending(sender As Object, e As SuspendingEventArgs) Handles Me.Suspending
        Dim deferral As SuspendingDeferral = e.SuspendingOperation.GetDeferral()
        ' TODO: Anwendungszustand speichern und alle Hintergrundaktivitäten beenden
        App.Logger.Write("Suspending...")

        Try
            Await RecipeBrowser.Common.SuspensionManager.SaveAsync()
        Catch ex As Exception
            Logger.Write("Suspension manager error:" + ex.ToString)
        End Try

        'For Each cur In BackgroundTaskRegistration.AllTasks
        '    cur.Value.Unregister(True)
        'Next

        deferral.Complete()
    End Sub

    Private Sub App_OnBackrequested(sender As Object, e As Windows.UI.Core.BackRequestedEventArgs)

        Dim rootFrame As Frame = TryCast(Window.Current.Content, Frame)

        If rootFrame Is Nothing Then
            Return
        End If

        Dim controlledFrame As Frame

        If MainPage.ContentFrame Is Nothing Then
            controlledFrame = rootFrame
        Else
            controlledFrame = MainPage.ContentFrame
        End If

        If controlledFrame.CanGoBack And e.Handled = False Then
            e.Handled = True
            controlledFrame.GoBack()
        End If
    End Sub

    Private Sub OnNavigatedTo(e As NavigationEventArgs)

        Dim rootFrame As Frame = Window.Current.Content
        Dim myPages As String = ""

        For Each page In rootFrame.BackStack
            myPages = myPages + page.SourcePageType.ToString() + "\n"
        Next

        If rootFrame.CanGoBack Then
            SystemNavigationManager.GetForCurrentView().AppViewBackButtonVisibility = AppViewBackButtonVisibility.Visible
        Else
            SystemNavigationManager.GetForCurrentView().AppViewBackButtonVisibility = AppViewBackButtonVisibility.Collapsed
        End If
    End Sub
End Class
