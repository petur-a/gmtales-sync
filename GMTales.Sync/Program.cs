using CommandLine;
using GMTales.Sync;

await Parser.Default.ParseArguments<Settings>(args)
    .WithParsedAsync(async settings =>
    {
        if (settings.StdIn)
        {
            Console.Write("Input password: ");
            settings.Password = Console.ReadLine();
        }

        if (string.IsNullOrEmpty(settings.Password))
        {
            Console.WriteLine("Password is empty, exiting");
            return;
        }

        try
        {
            var sync = new Sync(settings);
            await sync.ProcessArticles();
        }
        catch (Exception e)
        {
            Console.WriteLine($"An error occured. Message: {e.Message}");
        }
    });
