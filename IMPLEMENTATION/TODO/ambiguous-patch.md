the code currently registers two routes for PATCH and I get

```
Microsoft.AspNetCore.Routing.Matching.AmbiguousMatchException: The request matched multiple endpoints. Matches:

HTTP: PATCH /employments/{key} => TransitionEmployment
HTTP: PATCH /employments/{key} => UpdateEmployment
```

this is what the spec says but as the implementation shows, it is ambiguous.

how can we modify the spec to distinguish. would it be proper to use PUT for one?
Or should the secodn be be a PUT on "state" : PUT /employments/{key}/\_state
