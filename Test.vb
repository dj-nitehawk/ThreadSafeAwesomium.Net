Module Test

    Sub Main()

        Browser.StartWebCore()

        Using A As New Browser()

            Dim sw As New Stopwatch
            sw.Start()
            Console.WriteLine(A.GetRenderedHTML("http://google.com"))
            Console.WriteLine("Time Taken: " + sw.Elapsed.TotalSeconds.ToString("0"))
            Console.ReadLine()
            sw.Reset()
            sw.Start()
            Console.WriteLine(A.GetRenderedHTML("http://www.amazon.com/dp/B00LF10KNA"))
            Console.WriteLine("Time Taken: " + sw.Elapsed.TotalSeconds.ToString("0"))
            Console.ReadLine()
            sw.Stop()

        End Using

        Browser.StopWebCore()

    End Sub

End Module


