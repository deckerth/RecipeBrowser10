Imports Windows.Foundation.Diagnostics
Imports Windows.Storage

Public Class Logging
    Implements IDisposable

    Private Session As FileLoggingSession
    Private Channel As LoggingChannel

    Public Function IsEnabled() As Boolean
        Return Session IsNot Nothing
    End Function

    Public Sub Initialize()

        Dim settings = Windows.Storage.ApplicationData.Current.LocalSettings

        Dim active = settings.Values("LoggingActive")

        CheckDisposed()

        If active IsNot Nothing AndAlso active.Equals(Boolean.TrueString) Then
            If Not IsEnabled() Then
                Session = New FileLoggingSession("RecipeBrowserLog")
                AddHandler Session.LogFileGenerated, AddressOf LogFileGeneratedHandler
                Channel = New LoggingChannel("Events")
                Session.AddLoggingChannel(Channel, LoggingLevel.Information)
            End If
        Else
            If Session IsNot Nothing Then
                Session.Dispose()
                Session = Nothing
            End If
            If Channel IsNot Nothing Then
                Channel.Dispose()
                Channel = Nothing
            End If
        End If

    End Sub

    Public Async Sub SetActive(isActive As Boolean)

        Dim settings = Windows.Storage.ApplicationData.Current.LocalSettings

        CheckDisposed()

        settings.Values("LoggingActive") = isActive.ToString

        If Not isActive And Session IsNot Nothing Then
            Await CloseSessionSaveFinalLogFile()
        End If

        Initialize()

    End Sub

    Public Sub Write(ByRef Message As String)

        If Channel IsNot Nothing Then
            CheckDisposed()
            Channel.LogMessage(Message, LoggingLevel.Information)
        End If

    End Sub

    Private Function GetTimeStamp() As String

        Dim now As DateTime = DateTime.Now
        Return String.Format(System.Globalization.CultureInfo.InvariantCulture,
                                 "{0:D2}{1:D2}{2:D2}-{3:D2}{4:D2}{5:D2}{6:D3}",
                                 now.Year - 2000,
                                 now.Month,
                                 now.Day,
                                 now.Hour,
                                 now.Minute,
                                 now.Second,
                                 now.Millisecond)

    End Function

    Private Async Sub LogFileGeneratedHandler(sender As IFileLoggingSession, args As LogFileGeneratedEventArgs)
        Dim sampleAppDefinedLogFolder As StorageFolder = Await ApplicationData.Current.LocalFolder.CreateFolderAsync("RecipeBrowserLogs", CreationCollisionOption.OpenIfExists)
        Dim newLogFileName As String = "Log-" + GetTimeStamp() + ".etl"
        Await args.File.MoveAsync(sampleAppDefinedLogFolder, newLogFileName)
    End Sub

    Private Async Function CloseSessionSaveFinalLogFile() As Task(Of String)

        ' Save the final log file before closing the session.
        Dim finalFileBeforeSuspend As StorageFile = Await Session.CloseAndSaveToFileAsync()
        If finalFileBeforeSuspend IsNot Nothing Then
            ' Get the the app-defined log file folder. 
            Dim sampleAppDefinedLogFolder As StorageFolder = Await ApplicationData.Current.LocalFolder.CreateFolderAsync("RecipeBrowserLogs", CreationCollisionOption.OpenIfExists)
            ' Create a New log file name based on a date/time stamp.
            Dim newLogFileName As String = "Log-" + GetTimeStamp() + ".etl"
            'Move the final log into the app-defined log file folder. 
            Await finalFileBeforeSuspend.MoveAsync(sampleAppDefinedLogFolder, newLogFileName)
            ' Return the path to the log folder.
            Return System.IO.Path.Combine(sampleAppDefinedLogFolder.Path, newLogFileName)
        Else
            Return Nothing
        End If
    End Function

    Public Async Function PrepareToSuspendAsync() As Task

        CheckDisposed()

        If Session IsNot Nothing Then
            Try
                ' Before suspend, save any final log file.
                Await CloseSessionSaveFinalLogFile()
                Session.Dispose()
                Session = Nothing
            Catch ex As Exception
            End Try
        End If

    End Function

    Private Async Sub OnAppSuspending(sender As Object, e As Windows.ApplicationModel.SuspendingEventArgs)

        'Get a deferral before performing any async operations
        'to avoid suspension prior to LoggingScenario completing 
        'PrepareToSuspendAsync().
        Dim Deferral = e.SuspendingOperation.GetDeferral()

        Write("App suspending")

        'Prepare logging for suspension.
        Await PrepareToSuspendAsync()

        'From LoggingScenario's perspective, it's now okay to 
        'suspend, so release the deferral. 
        Deferral.Complete()
    End Sub

    Private Sub OnAppResuming(sender As Object, e As Object)

        'If logging was active at the last suspend,
        'ResumeLoggingIfApplicable will re-activate 
        'logging.
        Initialize()

        Write("App resumed")
    End Sub

    Public Sub New()

        AddHandler App.Current.Suspending, AddressOf OnAppSuspending
        AddHandler App.Current.Resuming, AddressOf OnAppResuming

        Initialize()

    End Sub

#Region "IDisposable Support"
    Private disposedValue As Boolean ' So ermitteln Sie überflüssige Aufrufe

    Private Sub CheckDisposed()
        If disposedValue Then
            Throw New ObjectDisposedException("LoggingScenario")
        End If
    End Sub

    ' IDisposable
    Protected Overridable Sub Dispose(disposing As Boolean)
        If Not Me.disposedValue Then
            ' TODO: Verwalteten Zustand löschen (verwaltete Objekte).
            If disposing Then
                If Channel IsNot Nothing Then
                    Channel.Dispose()
                End If
                If Session IsNot Nothing Then
                    Session.Dispose()
                End If
            End If
        End If
        Me.disposedValue = True
    End Sub


    ' Dieser Code wird von Visual Basic hinzugefügt, um das Dispose-Muster richtig zu implementieren.
    Public Sub Dispose() Implements IDisposable.Dispose
        ' Ändern Sie diesen Code nicht. Fügen Sie oben in Dispose(disposing As Boolean) Bereinigungscode ein.
        Dispose(True)
        GC.SuppressFinalize(Me)
    End Sub
#End Region

End Class
