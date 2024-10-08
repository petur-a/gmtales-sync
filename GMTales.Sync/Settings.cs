﻿using CommandLine;

namespace GMTales.Sync;

public class Settings
{
    [Option('c', "campaign", Required = true, HelpText = "Which campaign to synchronize with")]
    public int Campaign { get; set; }

    [Option('f', "folder", Required = true, HelpText = "From where to sync content")]
    public string? Folder { get; set; }

    [Option('u', "user", Required = true, HelpText = "Which user to use.")]
    public string? Username { get; set; }

    [Option('p', "password", Required = false, HelpText = "User password.")]
    public string? Password { get; set; }

    [Option('i', "std-in", Required = false, Default = false, HelpText = "Whether to read password from stdin")]
    public bool StdIn { get; set; }

    [Option("target", Required = false, Default = "https://api.gmtales.com", HelpText = "Where to sync to")]
    public string? BaseUrl { get; set; }
}
