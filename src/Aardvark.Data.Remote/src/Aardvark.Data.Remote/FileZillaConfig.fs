namespace Aardvark.Data.Remote

open System
open System.IO
open System.Xml.Linq

/// Utilities for parsing FileZilla configuration files
module FileZillaConfig =
    
    /// Parse FileZilla XML configuration content into SftpConfig
    let parseXml (xmlContent: string) : Result<SftpConfig, string> =
        try
            let doc = XDocument.Parse(xmlContent)

            let serverElement =
                doc.Descendants(XName.Get("Server"))
                |> Seq.tryHead

            match serverElement with
            | Some server ->
                try
                    let getElementValue name =
                        let element = server.Element(XName.Get(name))
                        if isNull element then "" else element.Value

                    let passElement = server.Element(XName.Get("Pass"))
                    let passEncoding = 
                        if isNull passElement then "" 
                        else 
                            let attr = passElement.Attribute(XName.Get("encoding"))
                            if isNull attr then "" else attr.Value
                    let passValue = if isNull passElement then "" else passElement.Value

                    let decodedPass =
                        if passEncoding = "base64" then
                            try
                                let bytes = Convert.FromBase64String(passValue)
                                System.Text.Encoding.UTF8.GetString(bytes)
                            with
                            | _ -> passValue  // If decoding fails, use value as-is
                        else
                            passValue

                    let portStr = getElementValue "Port"
                    let port = if String.IsNullOrEmpty(portStr) then 22 else int portStr

                    let config = {
                        Host = getElementValue "Host"
                        Port = port
                        User = getElementValue "User"
                        Pass = decodedPass
                    }

                    Ok config
                with
                | ex -> Error $"Error parsing server configuration: {ex.Message}"
                    
            | None ->
                Error "No Server element found in FileZilla configuration"
                
        with
        | ex -> Error $"Error parsing XML: {ex.Message}"

    /// Parse FileZilla configuration file into SftpConfig
    let parseFile (filePath: string) : Result<SftpConfig, string> =
        try
            if not (File.Exists filePath) then
                Error $"FileZilla configuration file not found: {filePath}"
            else
                let content = File.ReadAllText(filePath)
                parseXml content
        with
        | ex -> Error $"Error reading FileZilla configuration file: {ex.Message}"

    /// Try to parse FileZilla configuration, returning None on error
    let tryParseFile (filePath: string) : SftpConfig option =
        match parseFile filePath with
        | Ok config -> Some config
        | Error _ -> None

    /// Extract SFTP components from an SFTP URL
    let extractSftpComponents (sftpUrl: string) : Result<{| User: string; Host: string; Port: int; Path: string |}, string> =
        try
            let uri = Uri(sftpUrl)

            if uri.Scheme.ToLowerInvariant() <> "sftp" then
                Error "The URL is not an SFTP URL."
            else
                let user = if String.IsNullOrEmpty(uri.UserInfo) then "" else uri.UserInfo.Split(':')[0]
                let host = uri.Host
                let port = if uri.Port = -1 then 22 else uri.Port
                let path = uri.AbsolutePath

                Ok {| User = user; Host = host; Port = port; Path = path |}
        with
        | ex -> Error ex.Message