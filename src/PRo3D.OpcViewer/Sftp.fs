namespace PRo3D.OpcViewer

open System
open System.IO
open Renci.SshNet
open System.Xml.Linq

module Sftp =

    type SftpComponents = {
        User: string
        Server: string
        Port: int
        Path: string
    }

    let extractSftpComponents (sftpUrl: string) : Result<SftpComponents, string> =

        try
            let uri = Uri(sftpUrl)

            if uri.Scheme.ToLowerInvariant() <> "sftp" then
                Result.Error "The URL is not an SFTP URL."
            else
                let user = uri.UserInfo.Split(':')[0]
                let server = uri.Host
                let port = uri.Port
                let path = uri.AbsolutePath

                Result.Ok { User = user; Server = server; Port = port; Path = path }
        with
        | _ as e -> Result.Error e.Message

    let downloadFile (hostname: string) (port: int) (username: string) (password: string) (remoteFilePath: string) (localFilePath: string) =
        use sftp = new SftpClient(hostname, port, username, password)
        sftp.Connect()
        use fileStream = new FileStream(localFilePath, FileMode.Create)
        sftp.DownloadFile(remoteFilePath, fileStream)
        sftp.Disconnect()


    type SftpServerConfig = {
        Host: string
        Port: int
        //Protocol: int
        //Type: int
        User: string
        Pass: string
        //Logontype: int
        //EncodingType: string
        //BypassProxy: int
        //Name: string
        //SyncBrowsing: int
        //DirectoryComparison: int
    }
    with
        member this.DownloadFiles (remoteFilePaths: string seq, localBaseDir: string, progress : string -> unit) =
            
            let ensureDirectory (path : string) =
                Directory.CreateDirectory(Path.GetDirectoryName(path)) |> ignore
                path
        
            for remoteFilePath in remoteFilePaths do

                match extractSftpComponents remoteFilePath with
                    | Result.Ok components ->
                        // sftp://....
                        let localFilePath = localBaseDir + components.Path |> ensureDirectory
                        if not (File.Exists(localFilePath)) then
                            printfn "downloading %s" components.Path
                            printfn "  to %s" localFilePath
                            downloadFile this.Host this.Port this.User this.Pass components.Path localFilePath

                    | Result.Error msg ->
                        // try remoteFilePath as actual path ...
                        let localFilePath = localBaseDir + remoteFilePath |> ensureDirectory
                        downloadFile this.Host this.Port this.User this.Pass remoteFilePath localFilePath

                progress remoteFilePath

        member this.DownloadFile (remoteFilePath: string, localBaseDir: string, progress : string -> unit) =
            this.DownloadFiles([remoteFilePath], localBaseDir, progress)

        member this.DownloadFile (remoteFileUri: Uri, localBaseDir: string, progress : string -> unit) =
            this.DownloadFiles([remoteFileUri.ToString()], localBaseDir, progress)


    let parseFileZillaConfig (xmlContent: string) : SftpServerConfig =
        let doc = XDocument.Parse(xmlContent)

        let serverElement =
            doc.Descendants(XName.Get("Server"))
            |> Seq.head

        let getElementValue name =
            serverElement.Element(XName.Get(name)).Value

        let passElement = serverElement.Element(XName.Get("Pass"))
        let passEncoding = passElement.Attribute(XName.Get("encoding")).Value
        let passValue = passElement.Value

        let decodedPass =
            if passEncoding = "base64" then
                let bytes = Convert.FromBase64String(passValue)
                System.Text.Encoding.UTF8.GetString(bytes)
            else
                passValue // If encoding is not base64, return the value as is

        let config = {
            Host = getElementValue "Host"
            Port = getElementValue "Port" |> int
            //Protocol = getElementValue "Protocol" |> int
            //Type = getElementValue "Type" |> int
            User = getElementValue "User"
            Pass = decodedPass
            //Logontype = getElementValue "Logontype" |> int
            //EncodingType = getElementValue "EncodingType"
            //BypassProxy = getElementValue "BypassProxy" |> int
            //Name = getElementValue "Name"
            //SyncBrowsing = getElementValue "SyncBrowsing" |> int
            //DirectoryComparison = getElementValue "DirectoryComparison" |> int
        }

        config

    let parseFileZillaConfigFile (path : string) =
        parseFileZillaConfig (File.ReadAllText(path))