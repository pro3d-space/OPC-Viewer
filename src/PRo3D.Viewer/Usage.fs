namespace PRo3D.Viewer

open Argu

[<AutoOpen>]
module Usage =

    type CliArguments =
        | Version
        | DryRun
        | Screenshots of string
        | [<CliPrefix(CliPrefix.None)                       >] Diff of ParseResults<DiffCommand.Args>
        | [<CliPrefix(CliPrefix.None)                       >] Export of ParseResults<ExportCommand.Args>
        | [<CliPrefix(CliPrefix.None); AltCommandLine("ls") >] List of ParseResults<ListCommand.Args>
        | [<CliPrefix(CliPrefix.None)                       >] Project of ParseResults<ProjectCommand.Args>
        | [<CliPrefix(CliPrefix.None)                       >] View of ParseResults<ViewCommand.Args>

        interface IArgParserTemplate with
            member s.Usage =
                match s with
                | Version -> "Print program version."
                | DryRun -> "Parse arguments and output as JSON without executing commands."
                | Screenshots _ -> "Custom directory for saving screenshots (default: ./screenshots)."
                | Diff _ -> "Compute difference between a layer with other layers."
                | Export _ -> "Export data from datasets."
                | List _ -> "List datasets."
                | Project _ -> "Load configuration from JSON project file."
                | View _ -> "View datasets."



