Imports Awesomium.Core
'Imports HtmlAgilityPack

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
            'request.AppendExtraHeader("X-Forwarded-For", "137.135.166.225")
            Return Nothing
        End Function
    End Class

    Private Shared Thread As Threading.Thread
    Private Shared CoreIsRunning As Boolean = False
    Private Shared Session As WebSession
    Dim RenderedHTML As String = ""
    Dim RenderingDone As Boolean = False
    Dim View As WebView
    Dim jsOBJ As JSObject

    Private Shared Sub AwesomiumThread()
        If WebCore.IsInitialized = False And CoreIsRunning = False Then
            WebCore.Initialize(New WebConfig With {.LogLevel = LogLevel.None}, False)
            WebCore.Run(Sub(s, e)
                            WebCore.ResourceInterceptor = New Interceptor()
                            CoreIsRunning = True
                        End Sub)
        End If
    End Sub

    ''' <summary>
    ''' Call this to shutdown the dedicated awesomium thread and destroy any WebViews created by the WebCore.
    ''' Call it when your app is closing.
    ''' Once, shutdown, app restart is needed to use WebCore again.
    ''' </summary>
    Shared Sub StopWebCore()
        If Not IsNothing(Session) Then
            WebCore.DoWork(
                Function()
                    Session.Dispose()
                    Session = Nothing
                    Return Nothing
                End Function)
        End If
        WebCore.Shutdown()
        Thread = Nothing
        CoreIsRunning = False
    End Sub

    ''' <summary>
    ''' Call this to start awesomium webcore on it's own dedicated thread.
    ''' You can call this on app start but it's not needed as it will be called automatically when you create an instance of this class.
    ''' </summary>
    Shared Sub StartWebCore()
        If WebCore.IsInitialized = False And CoreIsRunning = False And IsNothing(Thread) Then
            Thread = New Threading.Thread(AddressOf AwesomiumThread)
            Thread.Start()
        End If
    End Sub

    Shared Sub ReduceWebCoreMemory()
        WebCore.ReleaseMemory()
    End Sub

    ''' <summary>
    ''' Create a new Browser class.
    ''' Don't forget to either use a USING statement or call DISPOSE after use.
    ''' </summary>
    Sub New()
        StartWebCore()
        InitSession()
        SetNewView()
    End Sub

    Private Shared Sub InitSession()
        If IsNothing(Session) Then
            Session = WebCore.DoWork(
                Function() As WebSession
                    Return WebCore.CreateWebSession(
                        New WebPreferences With {
                            .LoadImagesAutomatically = False,
                            .LocalStorage = False,
                            .Plugins = False,
                            .RemoteFonts = False,
                            .WebAudio = False,
                            .CanScriptsOpenWindows = False,
                            .DefaultEncoding = "utf-8",
                            .MaxHttpCacheStorage = 64000000,
                            .UserScript = "function SetSRC(){SRC=document.documentElement.outerHTML}function RemoveVideos(){var e=document.getElementsByTagName('video');for(index=e.length-1;index>=0;index--)e[index].parentNode.removeChild(e[index])}SRC='',window.addEventListener&&(window.addEventListener('DOMContentLoaded',RemoveVideos,!1),window.addEventListener('load',SetSRC,!1)),setTimeout(function(){jsOBJ.SendSRC(document.documentElement.outerHTML)},1e4);"})

                    'SRC = '';

                    'function SetSRC() {
                    '    SRC = document.documentElement.outerHTML;
                    '};

                    'function RemoveVideos() {
                    '    var element = document.getElementsByTagName('video');
                    '    for (index = element.length - 1; index >= 0; index--) {
                    '        element[index].parentNode.removeChild(element[index]);
                    '    };
                    '};

                    'if (window.addEventListener) {
                    '    window.addEventListener('DOMContentLoaded', RemoveVideos, false);
                    '    window.addEventListener("load", SetSRC, false);
                    '};

                    'setTimeout(function() {
                    '    jsOBJ.SendSRC(document.documentElement.outerHTML);
                    '}, 10000);

                End Function)
        End If

    End Sub

    Private Sub SetNewView()
        If Not IsNothing(View) Then
            RemoveHandler View.LoadingFrameFailed, Nothing
            RemoveHandler View.DocumentReady, Nothing
            WebCore.DoWork(Function()
                               If Not IsNothing(View) Then
                                   View.Dispose()
                               End If
                               Return Nothing
                           End Function)
            View = Nothing
        End If
        View = WebCore.DoWork(Function() As WebView
                                  Return WebCore.CreateWebView(1000,
                                                               500,
                                                               Session,
                                                               WebViewType.Offscreen)
                              End Function)

        AddHandler View.DocumentReady, Sub(s, e)
                                           If IsNothing(jsOBJ) Then
                                               View.SynchronousMessageTimeout = 3000
                                               jsOBJ = View.CreateGlobalJavascriptObject("jsOBJ")
                                               jsOBJ.BindAsync(
                                                   "SendSRC", Sub(sen, eve)
                                                                  If Not RenderingDone Then
                                                                      RenderedHTML = eve.Arguments(0).ToString
                                                                      Debug.WriteLine("SRC READY: " + View.Source.ToString)
                                                                      RenderingDone = True
                                                                  End If
                                                              End Sub)
                                           End If
                                           If RenderingDone = False And View.IsDocumentReady = True Then
                                               RenderedHTML = View.ExecuteJavascriptWithResult("SRC").ToString
                                               If Not String.IsNullOrEmpty(RenderedHTML) Then
                                                   Debug.WriteLine("SRC READY: " + View.Source.ToString)
                                                   RenderingDone = True
                                               End If
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

        RenderingDone = False
        View.Invoke(Sub()
                        Do Until View.IsLive
                            Task.Delay(100).Wait()
                        Loop
                        Debug.WriteLine("INVOKING: " + URL)
                        View.Source = URL.ToUri
                    End Sub)

        Dim startTime As Date = Date.UtcNow
        Do Until RenderingDone = True
            If Date.UtcNow.Subtract(startTime).TotalSeconds >= 15 Then
                View.Invoke(Sub()
                                View.Stop()
                            End Sub)
                Debug.WriteLine("TERMINATING: " + URL)
                Exit Do
            End If
            Task.Delay(300).Wait()
        Loop

        Debug.WriteLine("RENDER DONE: " + URL)

        If String.IsNullOrEmpty(RenderedHTML) Or RenderedHTML.Equals("undefined", StringComparison.OrdinalIgnoreCase) Then
            Debug.WriteLine("RENDER OUTPUT NULL: " + URL)
            Throw New Exception("Rendering failed!")
        End If

        'Dim doc As New HtmlAgilityPack.HtmlDocument()
        'doc.LoadHtml(RenderedHTML)
        'Dim body As HtmlNode = doc.DocumentNode.SelectSingleNode("//*[name() = 'body']")
        'If IsNothing(body) Then
        '    Debug.WriteLine("NO BODY TAG: " + URL)
        '    Throw New Exception("No body tag found!")
        'End If
        'If String.IsNullOrWhiteSpace(body.InnerHtml) Then
        '    Debug.WriteLine("EMPTY BODY: " + URL)
        '    Throw New Exception("Empty body content!")
        'End If

        Return RenderedHTML
    End Function

#Region "IDisposable Support"
    Private disposedValue As Boolean
    Protected Overridable Sub Dispose(disposing As Boolean)
        If Not Me.disposedValue Then
            If disposing Then
                RemoveHandler View.LoadingFrameFailed, Nothing
                RemoveHandler View.DocumentReady, Nothing
                WebCore.QueueWork(Sub()
                                      If Not IsNothing(View) Then
                                          View.Dispose()
                                          View = Nothing
                                      End If
                                      If Not IsNothing(jsOBJ) Then
                                          jsOBJ.Dispose()
                                          jsOBJ = Nothing
                                      End If
                                  End Sub)
            End If
            RenderedHTML = Nothing
            RenderingDone = Nothing
        End If
        Me.disposedValue = True
    End Sub
    Public Sub Dispose() Implements IDisposable.Dispose
        Dispose(True)
        GC.SuppressFinalize(Me)
    End Sub
#End Region

End Class
