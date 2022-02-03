# SoapHttp
Allows interoperability with WCF services and clients using generated WCF Reference.

Allows registration of an existing WCF ServiceContract to a HttpListener. The HttpListener is able to deserialize the WCF Request Message, forward it to the interface, and send back the Response Message or the Exception. This allows easy upgrading of WCF Service-oriented projects to .Net 5.0 or .Net 6.0

This library might not work with your WCF Service. Be sure to test all available messages and verify if the WCF Service functions the same as before.

## Supported Features
- HTTPListener for accepting WCF Messages from clients
- Async service calls
- Wrapped/Non-wrapped RequestMessage/ResponseMessage


## Todo:
- Client for sending messages to WCF service

## Exclusions
- Header data is currently not used.
