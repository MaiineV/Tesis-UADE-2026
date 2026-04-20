# Foundation#0006 — Player Service Real

## Setup

1. **Create the bootstrap asset**
   - Right-click in Project window: `Create > Rollgeon > Bootstrap > Player Service`
   - Save as `Assets/Resources/Bootstrap/PlayerServiceBootstrap.asset`

2. **Wire into ServiceBootstrapSO**
   - Open the `ServiceBootstrapSO` asset
   - Drag `PlayerServiceBootstrap` into the `ExtraServices` list
   - Priority 30 ensures it registers before Energy (50)

3. **Verify in Play Mode**
   - Enter Play Mode from the Bootstrap scene
   - Open the console — no errors related to `IPlayerService` should appear
   - The service is available via `ServiceLocator.GetService<IPlayerService>()`
