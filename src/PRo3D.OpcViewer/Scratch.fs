namespace PRo3D.OpcViewer

open PRo3D.OpcViewer
open System.IO

module Scratch =

    do
    
        let basedir = @"e:\tmp2"

        let server = Sftp.parseFileZillaConfig (File.ReadAllText(@"W:\Datasets\Pro3D\confidential\2025-02-24_AI-Mars-3D\Mastcam-Z.xml"))
        let p = getDataRefFromString @"sftp://mastcam-z-admin@dig-sftp.joanneum.at:2200/Mission/0600/0610/Job_0610_8618-110-rad-AI-Test/result/Job_0610_8618-110-rad-AI-Test_opc.zip"
        let q = resolveDataPath basedir (Some server) p
        printfn "%A" q
        exit 0

        let test1 = getDataRefFromString @"foo/myfolder"
        let test2 = getDataRefFromString @"foo\myfolder"
        let test3 = getDataRefFromString @"E:\tmp\scenes"
        let test4 = getDataRefFromString @"E:\tmp\"
        let test5 = getDataRefFromString @"http://download.vrvis.at/acquisition/32987e2792e0/PRo3D/GardenCity.zip"
        let test6 = getDataRefFromString @"https://download.vrvis.at/acquisition/32987e2792e0/PRo3D/GardenCity.zip"
        let test7 = getDataRefFromString @"sftp://mastcam-z-admin@dig-sftp.joanneum.at:2200/Mission/0600/0610/Job_0610_8618-110-rad-AI-Test/result/Job_0610_8618-110-rad-AI-Test_opc.zip"
        let test8 = getDataRefFromString @"T:\tmp\acquisition/32987e2792e0/PRo3D/GardenCity.zip"

        let remoteFilePaths = [
            "sftp://mastcam-z-admin@dig-sftp.joanneum.at:2200/Mission/0600/0610/Job_0610_8618-110-rad-AI-Test/result/Job_0610_8618-110-rad-AI-Test_opc.zip"
            "sftp://mastcam-z-admin@dig-sftp.joanneum.at:2200/Mission/0600/0610/Job_0610_8618-110-rad-NoAI/result/Job_0610_8618-110-rad-NoAI_opc.zip"
            "sftp://mastcam-z-admin@dig-sftp.joanneum.at:2200/Mission/0300/0360/Job_0360_8390-079-rad-AI-Mars-3D-Test/result/Job_0360_8390-079-rad-AI-Mars-3D-Test_opc.zip"
            "sftp://mastcam-z-admin@dig-sftp.joanneum.at:2200/Mission/0300/0360/Job_0360_8390-079-rad-AI-Mars-3D-Test-NoAI/result/Job_0360_8390-079-rad-AI-Mars-3D-Test-NoAI_opc.zip"
            "sftp://mastcam-z-admin@dig-sftp.joanneum.at:2200/Mission/0300/0361/Job_0361_7105-079-rad/result/Job_0361_7105-079-rad_opc.zip"
            "sftp://mastcam-z-admin@dig-sftp.joanneum.at:2200/Mission/1300/1399/Job_1399_4068-110-rad/result-AI/Job_1399_4068-110-rad_opc.zip"
            "sftp://mastcam-z-admin@dig-sftp.joanneum.at:2200/Mission/1300/1399/Job_1399_4068-110-rad/result-NoAI/Job_1399_4068-110-rad_opc.zip"
            "sftp://mastcam-z-admin@dig-sftp.joanneum.at:2200/Mission/1300/1326/Job_1326_9396-110-rad/result/Job_1326_9396-110-rad_opc.zip"
            "sftp://mastcam-z-admin@dig-sftp.joanneum.at:2200/Mission/1300/1326/Job_1326_9397-110-rad/result/Job_1326_9397-110-rad_opc.zip"
            "sftp://mastcam-z-admin@dig-sftp.joanneum.at:2200/Mission/1300/1326/Job_1326_9396-9397-LBS/result/Job_1326_9396-9397-LBS_opc.zip"
            "sftp://mastcam-z-admin@dig-sftp.joanneum.at:2200/Mission/1300/1326/Job_1326_9397-110-rad-DA-V2-A/result/Job_1326_9397-110-rad-DA-V2-A_opc.zip"
            "sftp://mastcam-z-admin@dig-sftp.joanneum.at:2200/Mission/1300/1326/Job_1326_9396-9397-LBS-Adjust-DA2/result/Job_1326_9396-9397-LBS-Adjust-DA2_opc.zip"
            ]
    
        let server = Sftp.parseFileZillaConfig (File.ReadAllText(@"W:\Datasets\Pro3D\confidential\2025-02-24_AI-Mars-3D\Mastcam-Z.xml"))
        server.DownloadFiles(remoteFilePaths, basedir, printfn "%s")

        let demoDatasets = [
            "http://download.vrvis.at/acquisition/32987e2792e0/PRo3D/GardenCity.zip"
            "http://download.vrvis.at/acquisition/32987e2792e0/PRo3D/VictoriaCrater.zip"
            "http://download.vrvis.at/acquisition/32987e2792e0/PRo3D/Stimson_1087.zip"
            "http://download.vrvis.at/acquisition/32987e2792e0/PRo3D/Job_1275_005976_MSLMST_opc.zip"
            ]

        exit 0

    ()
