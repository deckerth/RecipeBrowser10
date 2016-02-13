Namespace Common

    ''' <summary>
    ''' SuspensionManager erfasst den globalen Sitzungszustand, um die Verwaltung der Prozesslebensdauer
    ''' für eine Anwendung zu vereinfachen.  Beachten, dass der Sitzungszustand bei einer Vielzahl von Bedingungen
    ''' automatisch gelöscht wird und niemals zum Speichern von Informationen verwendet werden sollte, die zwischen Sitzungen zwar bequem übertragen werden können,
    ''' jedoch beim Absturz der Anwendung gelöscht werden sollen oder
    ''' aktualisiert werden.
    ''' </summary>
    Friend NotInheritable Class SuspensionManager

        Private Shared _sessionState As New Dictionary(Of String, Object)()
        Private Shared _knownTypes As New List(Of Type)()
        Private Const sessionStateFilename As String = "_sessionState.xml"

        ''' <summary>
        ''' Bietet Zugriff auf den globalen Sitzungszustand für die aktuelle Sitzung.  Dieser Zustand wird
        ''' von <see cref="SaveAsync"/> serialisiert und von
        ''' <see cref="RestoreAsync"/> wiederhergestellt, sodass die Werte durch
        ''' <see cref="Runtime.Serialization.DataContractSerializer"/> serialisierbar sein müssen und so kompakt wie
        ''' möglich sein sollten.  Zeichenfolgen und andere eigenständige Datentypen werden dringend empfohlen.
        ''' </summary>
        Public Shared ReadOnly Property SessionState As Dictionary(Of String, Object)
            Get
                Return _sessionState
            End Get
        End Property

        Public Shared Sub ResetSessionState()
            _sessionState.Clear()
        End Sub

        ''' <summary>
        ''' Liste mit benutzerdefinierten Typen, die für <see cref="Runtime.Serialization.DataContractSerializer"/>
        ''' beim Lesen und Schreiben des Sitzungszustands bereitgestellt werden.  Diese ist zu Beginn leer, und zusätzliche Typen können zum
        ''' Anpassen des Serialisierungsvorgangs hinzugefügt werden.
        ''' </summary>
        Public Shared ReadOnly Property KnownTypes As List(Of Type)
            Get
                Return _knownTypes
            End Get
        End Property

        ''' <summary>
        ''' Den aktuellen <see cref="SessionState"/> speichern.  Alle <see cref="Frame"/>-Instanzen,
        ''' die bei <see cref="RegisterFrame"/> registriert wurden, behalten ebenfalls ihren aktuellen
        ''' Navigationsstapel bei, wodurch deren aktive <see cref="Page"/> eine Gelegenheit
        ''' zum Speichern des zugehörigen Zustands erhält.
        ''' </summary>
        ''' <returns>Eine asynchrone Aufgabe, die das Speichern des Sitzungszustands wiedergibt.</returns>
        Public Shared Async Function SaveAsync() As Task
            Try

                ' Navigationszustand für alle registrierten Rahmen speichern
                For Each weakFrameReference As WeakReference(Of Frame) In _registeredFrames
                    Dim frame As Frame = Nothing
                    If weakFrameReference.TryGetTarget(frame) Then
                        SaveFrameNavigationState(frame)
                    End If
                Next

                ' Sitzungszustand synchron serialisieren, um einen asynchronen Zugriff auf den freigegebenen
                ' Zustand zu vermeiden
                Dim sessionData As New MemoryStream()
                Dim serializer As New Runtime.Serialization.DataContractSerializer(GetType(Dictionary(Of String, Object)), _knownTypes)
                serializer.WriteObject(sessionData, _sessionState)

                ' Einen Ausgabedatenstrom für die SessionState-Datei abrufen und den Zustand asynchron schreiben
                Dim file As Windows.Storage.StorageFile = Await Windows.Storage.ApplicationData.Current.LocalFolder.CreateFileAsync(
                    sessionStateFilename, Windows.Storage.CreationCollisionOption.ReplaceExisting)
                Using fileStream As Stream = Await file.OpenStreamForWriteAsync()
                    sessionData.Seek(0, SeekOrigin.Begin)
                    Await sessionData.CopyToAsync(fileStream)
                End Using
            Catch ex As Exception
                Throw New SuspensionManagerException(ex)
            End Try
        End Function

        ''' <summary>
        ''' Stellt den zuvor gespeicherten <see cref="SessionState"/> wieder her.  Für alle <see cref="Frame"/>-Instanzen,
        ''' die bei <see cref="RegisterFrame"/> registriert wurden, wird ebenfalls der vorherige
        ''' Navigationszustand wiederhergestellt, wodurch deren aktive <see cref="Page"/> eine Gelegenheit zum Wiederherstellen
        ''' des Zustands erhält.
        ''' </summary>
        ''' <returns>Eine asynchrone Aufgabe, die das Lesen des Sitzungszustands wiedergibt.  Auf den
        ''' Inhalt von <see cref="SessionState"/> sollte erst zurückgegriffen werden, wenn diese Aufgabe
        ''' abgeschlossen ist.</returns>
        Public Shared Async Function RestoreAsync() As Task
            _sessionState = New Dictionary(Of String, Object)()

            Try

                ' Eingabedatenstrom für die SessionState-Datei abrufen
                Dim file As Windows.Storage.StorageFile = Await Windows.Storage.ApplicationData.Current.LocalFolder.GetFileAsync(sessionStateFilename)
                If file Is Nothing Then Return

                Using inStream As Windows.Storage.Streams.IInputStream = Await file.OpenSequentialReadAsync()

                    ' Sitzungszustand deserialisieren
                    Dim serializer As New Runtime.Serialization.DataContractSerializer(GetType(Dictionary(Of String, Object)), _knownTypes)
                    _sessionState = DirectCast(serializer.ReadObject(inStream.AsStreamForRead()), Dictionary(Of String, Object))
                End Using

                ' Alle registrierten Rahmen auf den gespeicherten Zustand wiederherstellen
                For Each weakFrameReference As WeakReference(Of Frame) In _registeredFrames
                    Dim frame As Frame = Nothing
                    If weakFrameReference.TryGetTarget(frame) Then
                        frame.ClearValue(FrameSessionStateProperty)
                        RestoreFrameNavigationState(frame)
                    End If
                Next
            Catch ex As Exception
                Throw New SuspensionManagerException(ex)
            End Try
        End Function

        Private Shared FrameSessionStateKeyProperty As DependencyProperty =
            DependencyProperty.RegisterAttached("_FrameSessionStateKey", GetType(String), GetType(SuspensionManager), Nothing)
        Private Shared FrameSessionStateProperty As DependencyProperty =
            DependencyProperty.RegisterAttached("_FrameSessionState", GetType(Dictionary(Of String, Object)), GetType(SuspensionManager), Nothing)
        Private Shared _registeredFrames As New List(Of WeakReference(Of Frame))()

        ''' <summary>
        ''' Registriert eine <see cref="Frame"/>-Instanz, um den zugehörigen Navigationsverlauf mithilfe von
        ''' <see cref="SessionState"/> speichern und wiederherstellen zu können.  Rahmen sollten direkt nach der Erstellung
        ''' registriert werden, wenn diese Bestandteil der Verwaltung des Sitzungszustands sind.  Wenn der
        ''' Zustand für den speziellen Schlüssel bereits wiederhergestellt wurde,
        ''' wird der Navigationsverlauf bei der Registrierung sofort wiederhergestellt.  Bei nachfolgenden Aufrufen von
        ''' <see cref="RestoreAsync"/> wird der Navigationsverlauf ebenfalls wiederhergestellt.
        ''' </summary>
        ''' <param name="frame">Eine Instanz, deren Navigationsverlauf von
        ''' <see cref="SuspensionManager"/></param>
        ''' <param name="sessionStateKey">Ein eindeutiger Schlüssel in <see cref="SessionState"/> zum
        ''' Speichern von navigationsbezogenen Informationen.</param>
        Public Shared Sub RegisterFrame(frame As Frame, sessionStateKey As String)
            If frame.GetValue(FrameSessionStateKeyProperty) IsNot Nothing Then
                Throw New InvalidOperationException("Frames can only be registered to one session state key")
            End If

            If frame.GetValue(FrameSessionStateProperty) IsNot Nothing Then
                Throw New InvalidOperationException("Frames must be either be registered before accessing frame session state, or not registered at all")
            End If

            ' Eine Abhängigkeitseigenschaft verwenden, um den Sitzungsschlüssel mit einem Rahmen zu verknüpfen, und eine Liste von Rahmen speichern, deren
            ' Navigationszustand verwaltet werden soll
            frame.SetValue(FrameSessionStateKeyProperty, sessionStateKey)
            _registeredFrames.Add(New WeakReference(Of Frame)(frame))

            ' Überprüfen, ob der Navigationszustand wiederhergestellt werden kann
            RestoreFrameNavigationState(frame)
        End Sub

        ''' <summary>
        ''' Hebt die Verknüpfung eines <see cref="Frame"/>, der zuvor durch <see cref="RegisterFrame"/> registriert wurde,
        ''' mit <see cref="SessionState"/> auf.  Alle zuvor erfassten Navigationszustände werden
        ''' entfernt.
        ''' </summary>
        ''' <param name="frame">Eine Instanz, deren Navigationsverlauf nicht mehr
        ''' verwaltet werden soll.</param>
        Public Shared Sub UnregisterFrame(frame As Frame)

            ' Sitzungszustand und Rahmen aus der Liste der Rahmen entfernen, deren Navigationszustand
            ' gespeichert wird (gemeinsam mit allen schwachen Verweisen, die nicht mehr erreichbar sind)
            SessionState.Remove(DirectCast(frame.GetValue(FrameSessionStateKeyProperty), String))
            _registeredFrames.RemoveAll(Function(weakFrameReference)
                                            Dim testFrame As Frame = Nothing
                                            Return Not weakFrameReference.TryGetTarget(testFrame) OrElse testFrame Is frame
                                        End Function)
        End Sub

        ''' <summary>
        ''' Bietet Speichermöglichkeit für den Sitzungszustand, der mit dem angegebenen <see cref="Frame"/> verknüpft ist.
        ''' Für Rahmen, die zuvor mit <see cref="RegisterFrame"/> registriert wurden, wird der
        ''' Sitzungszustand automatisch als Teil des globalen <see cref="SessionState"/>
        ''' gespeichert und wiederhergestellt.  Rahmen, die nicht registriert sind, verfügen über einen vorübergehenden Zustand,
        ''' der weiterhin nützlich sein kann, wenn Seiten wiederhergestellt werden, die aus dem
        ''' Navigationscache gelöscht wurden.
        ''' </summary>
        ''' <remarks>Apps können beim Verwalten des seitenspezifischen Zustands auf <see cref="NavigationHelper"/> zurückgreifen,
        ''' anstatt direkt mit dem Rahmensitzungszustand zu arbeiten.</remarks>
        ''' <param name="frame">Die Instanz, für die der Sitzungszustand gewünscht wird.</param>
        ''' <returns>Eine Auflistung des Zustands, für den der gleiche Serialisierungsmechanismus wie für
        ''' <see cref="SessionState"/>.</returns>
        Public Shared Function SessionStateForFrame(frame As Frame) As Dictionary(Of String, Object)
            Dim frameState As Dictionary(Of String, Object) = DirectCast(frame.GetValue(FrameSessionStateProperty), Dictionary(Of String, Object))

            If frameState Is Nothing Then
                Dim frameSessionKey As String = DirectCast(frame.GetValue(FrameSessionStateKeyProperty), String)
                If frameSessionKey IsNot Nothing Then
                    If Not _sessionState.ContainsKey(frameSessionKey) Then

                        ' Registrierte Rahmen geben den entsprechenden Sitzungszustand wieder.
                        _sessionState(frameSessionKey) = New Dictionary(Of String, Object)()
                    End If
                    frameState = DirectCast(_sessionState(frameSessionKey), Dictionary(Of String, Object))
                Else

                    ' Rahmen, die nicht registriert sind, verfügen über einen vorübergehenden Zustand
                    frameState = New Dictionary(Of String, Object)()
                End If
                frame.SetValue(FrameSessionStateProperty, frameState)
            End If
            Return frameState
        End Function

        Private Shared Sub RestoreFrameNavigationState(frame As Frame)
            Dim frameState As Dictionary(Of String, Object) = SessionStateForFrame(frame)
            If frameState.ContainsKey("Navigation") Then
                frame.SetNavigationState(DirectCast(frameState("Navigation"), String))
            End If
        End Sub

        Private Shared Sub SaveFrameNavigationState(frame As Frame)
            Dim frameState As Dictionary(Of String, Object) = SessionStateForFrame(frame)
            frameState("Navigation") = frame.GetNavigationState()
        End Sub

    End Class
    Public Class SuspensionManagerException
        Inherits Exception
        Public Sub New()
        End Sub

        Public Sub New(ByRef e As Exception)
            MyBase.New("SuspensionManager failed", e)
        End Sub
    End Class
End Namespace
