# Fas 2 – Avstämning

> Kort avstamp inför Fas 3. Max 1–2 rader per punkt + länkar/prints.

## 1) EF Core + SQLite (databasbevis)
- **Körda migrationer**: 
![image info](./migrations.png "migrations") 

- **Var SQLite-filen ligger**:  <br><br/>
![image info](./db.png "DB")

- **Seed/testdata**:  <br><br/>
![seed](./seed.png "seed")  <br><br/>
![seed users](./seedUsers.png "users")  <br><br/>
![seed categories](./seedCategories.png "categories")  <br><br/>

- **Hur man aktiverar det**:  <br><br/>
![Running seed](./runningSeeds.png "running")
<!-- Krav i projekt-PM: EF Core + SQLite. -->


## 2) API-kontrakt & felmodell
- **Lista 2–4 kärnendpoints + vilka statuskoder ni VERIFIERAR via .http**: <br><br/>
![register http](./registerhttp.png "register")  <br><br/>
![category http](./categoryhttp.png "category")  <br><br/>
![comment http](./commenthttp.png "comment")  <br><br/>


<!-- Statuskodspolicy + ProblemDetails enligt undervisningspolicyn. -->

## 3) ProblemDetails
- **Kort: var/när returnerar vi ProblemDetails (t.ex. 400/404)**:  <br><br/>
**Var**: I våra API endpoints och application usecases.  <br/>
**När**: Då API:et anropas genom interaktion med blazor.

## 4) Blazor – UI-flöde
- **1 gif/print som visar: lista → detalj → skapa → visa (mot API)**. <br><br/>
![blazor ui](./ui.png "ui")  <br><br/>
![blazor code](./blazor.png "blazor")  <br><br/>

## 5) Identity & roller
- **Bekräfta att registrering/inloggning fungerar + att skyddad endpoint kräver behörighet (401/403)**. <br><br/>
![admin panel](./adminpanel.png "admin")  <br><br/>
![access denied](./denied.png "denied")  <br><br/>

- **Kort: hur UI-gate syns (t.ex. dolda knappar)**. <br><br/>
![ui gate](./gate.png "gate")  <br><br/>

## 6) README & körning
- **Länk till README med 3 användarscenarier + körinstruktioner + seed/testdata**. <br>
https://github.com/amnaswag/ChatForum_GroupTwo/blob/9430a4a1063872e1de66923a49b00f03eeac4d53/README.md

## 7) PR-disciplin
- Länk till senaste merged PR mot `main` som rör ett kärnflöde. <br>
https://github.com/amnaswag/ChatForum_GroupTwo/pull/21 
