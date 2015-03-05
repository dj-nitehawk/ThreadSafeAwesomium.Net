﻿Imports Awesomium.Core

Public Class Browser
    Implements IDisposable

    Private Class Interceptor
        Implements IResourceInterceptor

        Public Function OnFilterNavigation(request As NavigationRequest) As Boolean Implements IResourceInterceptor.OnFilterNavigation
            Return False
        End Function

        Public Function OnRequest(request As ResourceRequest) As ResourceResponse Implements IResourceInterceptor.OnRequest
            If Not request.Method = "GET" Then
                Return Nothing
            End If
            request.AppendExtraHeader("Accept", "text/html,application/xhtml+xml,application/xml;q=0.9,image/webp,*/*;q=0.8")
            request.AppendExtraHeader("User-Agent", "Mozilla/5.0 (Windows NT 6.3; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/40.0.2214.111 Safari/537.36")
            request.AppendExtraHeader("Accept-Language", "en-US,en;q=0.8")
            Return Nothing
        End Function
    End Class

    Private Shared Thread As Threading.Thread
    Private Shared CoreIsRunning As Boolean = False
    Dim RenderedHTML As String = ""
    Dim RenderingDone As Boolean = False
    Dim Session As WebSession
    Dim View As WebView
    Dim CreationTime As Date

    Private Shared Sub AwesomiumThread()
        WebCore.Initialize(New WebConfig With {.LogLevel = LogLevel.None}, False)
        WebCore.Run(Sub(s, e)
                        WebCore.ResourceInterceptor = New Interceptor()
                        CoreIsRunning = True
                    End Sub)
    End Sub

    ''' <summary>
    ''' Call this to shutdown the dedicated awesomium thread and destroy any WebViews created by the WebCore.
    ''' Call it when your app is closing.
    ''' </summary>
    Shared Sub StopWebCore()
        WebCore.Shutdown()
        Thread = Nothing
        CoreIsRunning = Nothing
    End Sub

    ''' <summary>
    ''' Call this to start awesomium webcore on it's own dedicated thread.
    ''' You can call this on app start but it's not needed as it will be called automatically when you create an instance of this class.
    ''' </summary>
    Shared Sub StartWebCore()
        If Not WebCore.IsInitialized And IsNothing(Thread) Then
            Thread = New Threading.Thread(AddressOf AwesomiumThread)
            Thread.Start()
        End If
    End Sub

    ''' <summary>
    ''' Create a new Browser class.
    ''' Don't forget to either use a USING statement or call DISPOSE after use.
    ''' </summary>

    Sub New()
        StartWebCore()
        SetNewSession()
        SetNewView()
        CreationTime = Date.UtcNow
    End Sub

    Private Sub SetNewSession()
        If Not IsNothing(Session) Then
            WebCore.QueueWork(Sub()
                                  Session.Dispose()
                              End Sub)
            Session = Nothing
        End If
        Session = WebCore.DoWork(Function() As WebSession
                                     Return WebCore.CreateWebSession(
                                         New WebPreferences With {
                                             .LoadImagesAutomatically = False,
                                             .LocalStorage = False,
                                             .Plugins = False,
                                             .RemoteFonts = False,
                                             .WebAudio = False,
                                             .CanScriptsOpenWindows = False,
                                             .DefaultEncoding = "utf-8"})
                                 End Function)
    End Sub

    Private Sub SetNewView()
        If Not IsNothing(View) Then
            RemoveHandler View.LoadingFrameComplete, Nothing
            RemoveHandler View.LoadingFrameFailed, Nothing
            WebCore.QueueWork(Sub()
                                  View.Dispose()
                              End Sub)
            View = Nothing
        End If
        View = WebCore.DoWork(Function() As WebView
                                  Return WebCore.CreateWebView(1000,
                                                               500,
                                                               Session,
                                                               WebViewType.Offscreen)
                              End Function)
        AddHandler View.LoadingFrameComplete, Sub(s, e)
                                                  If e.IsMainFrame Then
                                                      RenderedHTML = View.ExecuteJavascriptWithResult("document.documentElement.outerHTML").ToString
                                                      RenderingDone = True
                                                  End If
                                              End Sub
        AddHandler View.LoadingFrameFailed, Sub(s, e)
                                                If e.IsMainFrame Then
                                                    RenderedHTML = ""
                                                    RenderingDone = True
                                                End If
                                            End Sub
    End Sub

    ''' <summary>
    ''' Returns a browser rendered final HTML source for a given URL, including dynamic content such as javascript.
    ''' </summary>
    ''' <param name="URL">A properly formatted valid URL in the form of "http://website.com/"</param>
    ''' <returns>Fully rendered valid HTML markup.</returns>
    Function GetRenderedHTML(URL As String) As String
        Do Until CoreIsRunning
            Task.Delay(100).Wait()
        Loop

        Dim ViewIsAlive As Boolean = View.Invoke(Function() As Boolean
                                                     Return View.IsLive
                                                 End Function)

        If (Date.UtcNow.Subtract(CreationTime).TotalMinutes >= 60) Or (Not ViewIsAlive) Then
            SetNewSession()
            SetNewView()
            CreationTime = Date.UtcNow
        End If

        RenderingDone = False
        View.Invoke(Sub()
                        View.Source = URL.ToUri
                    End Sub)

        Dim startTime As Date = Date.UtcNow
        Do Until RenderingDone = True
            If Date.UtcNow.Subtract(startTime).TotalSeconds >= 15 Then
                View.Invoke(Sub()
                                View.Stop()
                            End Sub)
                Exit Do
            End If
            Task.Delay(100).Wait()
        Loop

        If String.IsNullOrEmpty(RenderedHTML) Then
            Throw New Exception("Rendering failed!")
        End If

        Return RenderedHTML
    End Function

#Region "IDisposable Support"
    Private disposedValue As Boolean
    Protected Overridable Sub Dispose(disposing As Boolean)
        If Not Me.disposedValue Then
            If disposing Then
                RemoveHandler View.LoadingFrameComplete, Nothing
                RemoveHandler View.LoadingFrameFailed, Nothing
                WebCore.QueueWork(Sub()
                                      View.Dispose()
                                      Session.Dispose()
                                  End Sub)
            End If
            View = Nothing
            Session = Nothing
            RenderedHTML = Nothing
            RenderingDone = Nothing
            CreationTime = Nothing
        End If
        Me.disposedValue = True
    End Sub
    Public Sub Dispose() Implements IDisposable.Dispose
        Dispose(True)
        GC.SuppressFinalize(Me)
    End Sub
#End Region

End Class