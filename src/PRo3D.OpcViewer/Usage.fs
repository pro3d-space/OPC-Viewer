namespace PRo3D.OpcViewer

open Argu

[<AutoOpen>]
module Usage =

    type CliArguments =
        | Version
        | [<CliPrefix(CliPrefix.None); AltCommandLine("ls")>] List of ParseResults<ListCommand.Args>
        | [<CliPrefix(CliPrefix.None)>] View of ParseResults<ViewCommand.Args>

        interface IArgParserTemplate with
            member s.Usage =
                match s with
                | Version -> "Print program version."
                | List _ -> "List datasets."
                | View _ -> "View datasets."



