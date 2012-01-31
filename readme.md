# RemoteSigner
Sign executable file with a remote server.

## Introduction
This software is a simple suite that consists of client and server, where client submits an executable file to the server, server signs the file and send back to the client.

## Build
This software is written in Visual Basic .NET 2010. In this regard, you should install Visual Studio 2010 or higher.

## Prerequisite
The server requires the sign tool from TrustAsia: https://www.trustasia.com/download-sign-tools <br>
Import your certificate and create a signing rule for it. Otherwise, the tool cannot sign. <br>
You are supposed to adjust system time in order to sign files with expired certificates. <br>
Both server and client requires `.NET Framework 4.0`. Download it from Microsoft: https://dotnet.microsoft.com/download/dotnet-framework/net40

## Protocol
The protocol offers neither authentication nor encryption, and thereby this software is supposed to be used for personal use inside private LAN. <br>
The protocol explication is written in client-side perspective with C pseudo-code. <br>
The client initiate TCP connection to the server's port 1125. Then send a request header to the server. The structure of request header may look like:
```C
typedef struct _RS_HEADER
{
    char Signature[16];     // ASCII Char-set
    int FileSize;           // Size in bytes
}RS_HEADER,*PRS_HEADER;
```
To sign a file, you should specify the `Signature` to be `Sign Request #ZT`. <br>
Then the client should send the rule name to the server. The rule name is referring to the rule you created. The structure of rule name may look like:
```C
typedef struct _RS_RULE_NAME
{
    char RuleName[255];     // ASCII Char-set
    unsigned char Length;   // Size in bytes
}RS_RULE_NAME,*PRS_RULE_NAME;
```
The last thing to send is of course the executable file content. Once it is sent, you should receive a reply header with the same structure as request header. <br>
If the signing process succeeded, the `Signature` would equal to `Sign Reply #ZT-S`. <br>
Otherwise, the `Signature` would equal to `Sign Reply #ZT-F`. <br>
The remaining stuff to receive is the signed executable file.

## Time
This software would require you to adjust the system time in order to sign the executable successfully. In order not to be confused when reading the server log, you may choose to query Internet time. However, doing so may cause a long-time hang. 

## License
This software is licensed under the `Microsoft Public License`.