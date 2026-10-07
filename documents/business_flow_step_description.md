
## **Swimlane Steps — Main Flow (New Version)**

### 

| \# | Actor / Lane | Step | Description |
| :---: | :---- | :---- | :---- |
| 1 | USER | Open AI page · capture / upload waste photo | The user opens the EcoLink AI scan page and takes a photo of the waste or uploads an existing image to start the recognition process |
| 2 | ECOLINK SYSTEM | Process image & object recognition | The system pre-processes the uploaded image and runs object detection to locate every waste item inside the photo |
| 3 | ECOLINK SYSTEM | Classify · score · weigh | The system classifies each detected item into a waste category, assigns a confidence score and estimates its weight |
| 4 | ECOLINK SYSTEM | Return result & material suggestion | The system returns the recognised waste categories, scores, estimated weight and the suggested sorting approach |
| 5 | USER | Review result · correct waste type | The user checks the recognised result and corrects any wrong waste type or weight before looking for a recycler |
| 6 | ECOLINK SYSTEM | Update per-user waste catalog | The system stores the corrections of the user so that the personal waste catalog and later classifications become more accurate |
| 7 | USER | Tap “Find nearby recyclers” | The user starts a map search for recyclers and large buyers who can accept the selected waste types |
| 8 | ECOLINK SYSTEM | Search recyclers on map | The system queries the recyclers and buyers whose accepted waste list matches the request and ranks them by distance |
| 9 | ECOLINK SYSTEM | Return recycler list \+ quotes | The system returns the matching recyclers on the map together with their price quote for the selected waste |
| 10 | USER | Select recycler · view price | The user selects one recycler from the map and views their profile, accepted waste types and price list |
| 11 | RECYCLER / BUYER | Receive request · view quote · reply | The recycler receives the handover request, reviews the waste types and quantities, and replies with a quote |
| 12 | USER | Confirm deal (online on web) | The user confirms the handover order on the website with the chosen recycler, waste types, weight and pickup address |
| 13 | ECOLINK SYSTEM | Create pickup appointment in chat | The system creates the pickup appointment in the negotiation chat as soon as the deal is confirmed |
| 14 | RECYCLER / BUYER | Confirm & agree pickup time | The recycler confirms the order and agrees with the user on the date and time of the pickup in the chat |
| 15 | ECOLINK SYSTEM | Receive and store chat transcript | The system receives and stores the negotiation messages as the official record of the transaction |
| 16 | RECYCLER / BUYER | Tap create-handover-confirmation | After collecting the waste, the recycler taps the button to send the handover confirmation to the user |
| 17 | ECOLINK SYSTEM | Create handover confirmation | The system creates the handover confirmation record and sends it to the user for verification |
| 18 | USER | Confirm handover | After the recycler arrives, the user confirms in the app that the waste has been handed over as agreed |
| 19 | USER | Decision: Confirmed? | Decision point. No — the user does not confirm, so the appointment is cancelled automatically. Yes — go to the rating step |
| 20 | ECOLINK SYSTEM | Auto-cancel after 30 min if appointment missed | If the user does not confirm within 30 minutes after the agreed pickup time, the system automatically cancels the appointment and closes the flow |
| 21 | ECOLINK SYSTEM | Open recycler rating | The system opens the rating form for the recycler of the completed handover |
| 22 | USER | Rate the recycler | The user rates the recycler with a star score and an optional comment after a successful handover |
| 23 | USER | Decision: Rated? | Decision point. Yes — the rating is saved and the recycler is notified. No — the flow ends without a rating |
| 24 | ECOLINK SYSTEM | Update and notify | The system saves the rating, updates the average score and rating count of the recycler, and sends the notification |
| 25 | RECYCLER / BUYER | Receive notification | The recycler receives the notification about the handover confirmation result and the new rating |

###  

### 