Namespace Common
    ''' <summary>
    '''  
    ''' </summary>
    ''' <remarks></remarks>
    Public Class NavigationHelper
        Inherits DependencyObject
        Private Property Page() As Page
        Private ReadOnly Property Frame() As Frame
            Get
                Return Me.Page.Frame
            End Get
        End Property

        Public Sub New(page As Page)
            Me.Page = page

            ' Zwei Änderungen vornehmen, wenn diese Seite Teil der visuellen Struktur ist:
            ' 1) Den Ansichtszustand der Anwendung dem visuellen Zustand für die Seite zuordnen
            ' 2) Tastatur- und Mausnavigationsanforderungen bearbeiten
            AddHandler Me.Page.Loaded,
                Sub(sender, e)
                    ' Tastatur- und Mausnavigation trifft nur zu, wenn das gesamte Fenster ausgefüllt wird.
                    If Me.Page.ActualHeight = Window.Current.Bounds.Height AndAlso Me.Page.ActualWidth = Window.Current.Bounds.Width Then
                        ' Direkt am Fenster lauschen, sodass kein Fokus erforderlich ist
                        AddHandler Window.Current.CoreWindow.Dispatcher.AcceleratorKeyActivated, AddressOf CoreDispatcher_AcceleratorKeyActivated
                        AddHandler Window.Current.CoreWindow.PointerPressed, AddressOf Me.CoreWindow_PointerPressed
                    End If
                End Sub

            ' Dieselben Änderungen rückgängig machen, wenn die Seite nicht mehr sichtbar ist
            AddHandler Me.Page.Unloaded,
                Sub(sender, e)
                    RemoveHandler Window.Current.CoreWindow.Dispatcher.AcceleratorKeyActivated, AddressOf CoreDispatcher_AcceleratorKeyActivated
                    RemoveHandler Window.Current.CoreWindow.PointerPressed, AddressOf Me.CoreWindow_PointerPressed
                End Sub
        End Sub

#Region "Navigationsunterstützung dient."

        Private _goBackCommand As RelayCommand
        Public Property GoBackCommand() As RelayCommand
            Get
                If _goBackCommand Is Nothing Then
                    _goBackCommand = New RelayCommand(AddressOf Me.GoBack, AddressOf Me.CanGoBack)
                End If
                Return _goBackCommand
            End Get
            Set(value As RelayCommand)
                _goBackCommand = value
            End Set
        End Property

        Private _goForwardCommand As RelayCommand
        Public ReadOnly Property GoForwardCommand() As RelayCommand
            Get
                If _goForwardCommand Is Nothing Then
                    _goForwardCommand = New RelayCommand(AddressOf Me.GoForward, AddressOf Me.CanGoForward)
                End If
                Return _goForwardCommand
            End Get
        End Property

        Public Overridable Function CanGoBack() As Boolean
            Return Me.Frame IsNot Nothing AndAlso Me.Frame.CanGoBack
        End Function
        Public Overridable Function CanGoForward() As Boolean
            Return Me.Frame IsNot Nothing AndAlso Me.Frame.CanGoForward
        End Function

        Public Overridable Sub GoBack()
            If Me.Frame IsNot Nothing AndAlso Me.Frame.CanGoBack Then
                Me.Frame.GoBack()
            End If
        End Sub
        Public Overridable Sub GoForward()
            If Me.Frame IsNot Nothing AndAlso Me.Frame.CanGoForward Then
                Me.Frame.GoForward()
            End If
        End Sub

        ''' <summary>
        ''' Wird bei jeder Tastatureingabe aufgerufen, einschließlich Systemtasten wie ALT-Tastenkombinationen, wenn
        ''' diese Seite aktiv ist und das gesamte Fenster ausfüllt.  Wird zum Erkennen von Tastaturnavigation verwendet
        ''' zwischen Seiten, auch wenn die Seite selbst nicht den Fokus hat.
        ''' </summary>
        ''' <param name="sender">Instanz, von der das Ereignis ausgelöst wurde.</param>
        ''' <param name="e">Ereignisdaten, die die Bedingungen beschreiben, die zu dem Ereignis geführt haben.</param>
        Private Sub CoreDispatcher_AcceleratorKeyActivated(sender As Windows.UI.Core.CoreDispatcher,
                                                           e As Windows.UI.Core.AcceleratorKeyEventArgs)
            Dim virtualKey As Windows.System.VirtualKey = e.VirtualKey

            ' Weitere Untersuchungen nur durchführen, wenn die Taste "Nach links", "Nach rechts" oder die dezidierten Tasten "Zurück" oder "Weiter"
            ' gedrückt werden
            If (e.EventType = Windows.UI.Core.CoreAcceleratorKeyEventType.SystemKeyDown OrElse
                e.EventType = Windows.UI.Core.CoreAcceleratorKeyEventType.KeyDown) AndAlso
                (virtualKey = Windows.System.VirtualKey.Left OrElse
                virtualKey = Windows.System.VirtualKey.Right OrElse
                virtualKey = 166 OrElse
                virtualKey = 167) Then

                ' Bestimmen, welche Zusatztasten gedrückt gehalten wurden
                Dim coreWindow As Windows.UI.Core.CoreWindow = Window.Current.CoreWindow
                Dim downState As Windows.UI.Core.CoreVirtualKeyStates = Windows.UI.Core.CoreVirtualKeyStates.Down
                Dim menuKey As Boolean = (coreWindow.GetKeyState(Windows.System.VirtualKey.Menu) And downState) = downState
                Dim controlKey As Boolean = (coreWindow.GetKeyState(Windows.System.VirtualKey.Control) And downState) = downState
                Dim shiftKey As Boolean = (coreWindow.GetKeyState(Windows.System.VirtualKey.Shift) And downState) = downState
                Dim noModifiers As Boolean = Not menuKey AndAlso Not controlKey AndAlso Not shiftKey
                Dim onlyAlt As Boolean = menuKey AndAlso Not controlKey AndAlso Not shiftKey

                If (virtualKey = 166 AndAlso noModifiers) OrElse
                    (virtualKey = Windows.System.VirtualKey.Left AndAlso onlyAlt) Then

                    ' Wenn die Taste "Zurück" oder ALT+NACH-LINKS-TASTE gedrückt wird, zurück navigieren
                    e.Handled = True
                    Me.GoBackCommand.Execute(Nothing)
                ElseIf (virtualKey = 167 AndAlso noModifiers) OrElse
                    (virtualKey = Windows.System.VirtualKey.Right AndAlso onlyAlt) Then

                    ' Wenn die Taste "Weiter" oder ALT+NACH-RECHTS-TASTE gedrückt wird, vorwärts navigieren
                    e.Handled = True
                    Me.GoBackCommand.Execute(Nothing)
                End If
            End If
        End Sub

        ''' <summary>
        ''' Wird bei jedem Mausklick, jeder Touchscreenberührung oder einer äquivalenten Interaktion aufgerufen, wenn diese
        ''' Seite aktiv ist und das gesamte Fenster ausfüllt.  Wird zum Erkennen von "Weiter"- und "Zurück"-Maustastenklicks
        ''' im Browserstil verwendet, um zwischen Seiten zu navigieren.
        ''' </summary>
        ''' <param name="sender">Instanz, von der das Ereignis ausgelöst wurde.</param>
        ''' <param name="e">Ereignisdaten, die die Bedingungen beschreiben, die zu dem Ereignis geführt haben.</param>
        Private Sub CoreWindow_PointerPressed(sender As Windows.UI.Core.CoreWindow,
                                              e As Windows.UI.Core.PointerEventArgs)
            Dim properties As Windows.UI.Input.PointerPointProperties = e.CurrentPoint.Properties

            ' Tastenkombinationen mit der linken, rechten und mittleren Taste ignorieren
            If properties.IsLeftButtonPressed OrElse properties.IsRightButtonPressed OrElse
                properties.IsMiddleButtonPressed Then Return

            ' Wenn "Zurück" oder "Vorwärts" gedrückt wird (jedoch nicht gleichzeitig), entsprechend navigieren
            Dim backPressed As Boolean = properties.IsXButton1Pressed
            Dim forwardPressed As Boolean = properties.IsXButton2Pressed
            If backPressed Xor forwardPressed Then
                e.Handled = True
                If backPressed Then Me.GoBackCommand.Execute(Nothing)
                If forwardPressed Then Me.GoForwardCommand.Execute(Nothing)
            End If
        End Sub

#End Region

#Region "Verwaltung der Prozesslebensdauer"

        Private _pageKey As String

        ''' <summary>
        ''' Wird aufgerufen, wenn diese Seite in einem Rahmen angezeigt werden soll.
        ''' </summary>
        ''' <param name="e">Ereignisdaten, die beschreiben, wie diese Seite erreicht wurde.  Die
        ''' Parametereigenschaft stellt die anzuzeigende Gruppe bereit.</param>
        Public Sub OnNavigatedTo(e As Navigation.NavigationEventArgs)
            Dim frameState As Dictionary(Of String, Object) = SuspensionManager.SessionStateForFrame(Me.Frame)
            Me._pageKey = "Page-" & Me.Frame.BackStackDepth

            If e.NavigationMode = Navigation.NavigationMode.New Then

                ' Bestehenden Zustand für die Vorwärtsnavigation löschen, wenn dem Navigationsstapel eine neue
                ' Seite hinzugefügt wird
                Dim nextPageKey As String = Me._pageKey
                Dim nextPageIndex As Integer = Me.Frame.BackStackDepth
                While (frameState.Remove(nextPageKey))
                    nextPageIndex += 1
                    nextPageKey = "Page-" & nextPageIndex
                End While


                ' Den Navigationsparameter an die neue Seite übergeben
                RaiseEvent LoadState(Me, New LoadStateEventArgs(e.Parameter, Nothing))
            Else

                ' Den Navigationsparameter und den beibehaltenen Seitenzustand an die Seite übergeben,
                ' dabei die gleiche Strategie verwenden wie zum Laden des angehaltenen Zustands und zum erneuten Erstellen von im Cache verworfenen
                ' Seiten
                RaiseEvent LoadState(Me, New LoadStateEventArgs(e.Parameter, DirectCast(frameState(Me._pageKey), Dictionary(Of String, Object))))
            End If
        End Sub

        ''' <summary>
        ''' Wird aufgerufen, wenn diese Seite nicht mehr in einem Rahmen angezeigt wird.
        ''' </summary>
        ''' <param name="e">Ereignisdaten, die beschreiben, wie diese Seite erreicht wurde.  Die
        ''' Parametereigenschaft stellt die anzuzeigende Gruppe bereit.</param>
        Public Sub OnNavigatedFrom(e As Navigation.NavigationEventArgs)
            Dim frameState As Dictionary(Of String, Object) = SuspensionManager.SessionStateForFrame(Me.Frame)
            Dim pageState As New Dictionary(Of String, Object)()
            RaiseEvent SaveState(Me, New SaveStateEventArgs(pageState))
            frameState(_pageKey) = pageState
        End Sub

        Public Event LoadState As LoadStateEventHandler
        Public Event SaveState As SaveStateEventHandler
#End Region

    End Class

    Public Class LoadStateEventArgs
        Inherits EventArgs

        Public Property NavigationParameter() As Object
        Public Property PageState() As Dictionary(Of String, Object)

        Public Sub New(navigationParameter As Object, pageState As Dictionary(Of String, Object))
            MyBase.New()
            Me.NavigationParameter = navigationParameter
            Me.PageState = pageState
        End Sub
    End Class
    Public Delegate Sub LoadStateEventHandler(sender As Object, e As LoadStateEventArgs)

    Public Class SaveStateEventArgs
        Inherits EventArgs

        Public Property PageState() As Dictionary(Of String, Object)

        Public Sub New(pageState As Dictionary(Of String, Object))
            MyBase.New()
            Me.PageState = pageState
        End Sub

    End Class

    Public Delegate Sub SaveStateEventHandler(sender As Object, e As SaveStateEventArgs)
End Namespace