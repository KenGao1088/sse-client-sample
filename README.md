# run
Fire up the SSE server first, then run this:
```
dotnet run
```

# receiving message

send the following command to server
```
curl -XPOST https://localhost:5001/api/notification/send -k -d'{"name":"user1","message":"hello this is a test message"}' -H'Content-Type: application/json'
```

# upload file

Enter `upload <filename>` once the application is up to upload. A link will be generated once upload completes.

# note
On a linux system with dotnet core 2.1, you need to add this environment variable:
```
export CLR_OPENSSL_VERSION_OVERRIDE=1.1
```
