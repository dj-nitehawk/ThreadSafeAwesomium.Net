Module Test

    Sub Main()

        Browser.StartWebCore()

        Dim sw As New Stopwatch
        sw.Start()

        Using A As New Browser()

            Console.WriteLine(A.GetRenderedHTML("http://www.kimsufi.com/uk/"))
            Console.WriteLine("Time Taken: " + sw.Elapsed.TotalSeconds.ToString("0"))

            sw.Restart()

            Console.WriteLine(A.GetRenderedHTML("http://cnn.com"))
            Console.WriteLine("Time Taken: " + sw.Elapsed.TotalSeconds.ToString("0"))

            Console.ReadLine()

        End Using

        Browser.StopWebCore()

    End Sub

End Module


