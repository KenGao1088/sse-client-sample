# run
```
dotnet run
```

# receiving message

send the following command to server
```
curl -XPOST https://localhost:5001/api/notification/send -k -d'{"name":"user1","message":"hello this is a test message"}' -H'Content-Type: application/json'
```
