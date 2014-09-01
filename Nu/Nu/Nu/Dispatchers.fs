﻿namespace Nu
open System
open System.ComponentModel
open OpenTK
open Prime
open TiledSharp
open Nu
open Nu.NuConstants

[<AutoOpen>]
module EntityDispatcherModule =

    type Entity with
    
        static member private sortFstDesc (priority, _) (priority2, _) =
            if priority = priority2 then 0
            elif priority > priority2 then -1
            else 1

        static member mouseToEntity position world (entity : Entity) =
            let positionScreen = Camera.mouseToScreen position world.Camera
            let view = (if Entity.isTransformRelative entity world then Camera.getViewRelativeF else Camera.getViewAbsoluteF) world.Camera
            let positionEntity = positionScreen * view
            positionEntity

        static member setPositionSnapped snap position (entity : Entity) =
            let snapped = NuMath.snap2F snap position
            Entity.setPosition snapped entity

        static member getTransform (entity : Entity) =
            { Transform.Position = entity.Position
              Depth = entity.Depth
              Size = entity.Size
              Rotation = entity.Rotation }

        static member setTransform positionSnap rotationSnap transform (entity : Entity) =
            let transform = NuMath.snapTransform positionSnap rotationSnap transform
            entity |>
                Entity.setPosition transform.Position |>
                Entity.setDepth transform.Depth |>
                Entity.setSize transform.Size |>
                Entity.setRotation transform.Rotation

        static member pickingSort entities world =
            let priorities = List.map (fun (entity : Entity) -> Entity.getPickingPriority entity world) entities
            let prioritiesAndEntities = List.zip priorities entities
            let prioritiesAndEntitiesSorted = List.sortWith Entity.sortFstDesc prioritiesAndEntities
            List.map snd prioritiesAndEntitiesSorted

        static member tryPick position entities world =
            let entitiesSorted = Entity.pickingSort entities world
            List.tryFind
                (fun entity ->
                    let positionEntity = Entity.mouseToEntity position world entity
                    let transform = Entity.getTransform entity
                    let picked = NuMath.isPointInBounds3 positionEntity transform.Position transform.Size
                    picked)
                entitiesSorted

[<AutoOpen>]
module SimpleBodyFacetModule =

    type Entity with

        member entity.MinorId = entity?MinorId () : Guid
        static member setMinorId (value : Guid) (entity : Entity) : Entity = entity?MinorId <- value
        member entity.BodyType = entity?BodyType () : BodyType
        static member setBodyType (value : BodyType) (entity : Entity) : Entity = entity?BodyType <- value
        member entity.Density = entity?Density () : single
        static member setDensity (value : single) (entity : Entity) : Entity = entity?Density <- value
        member entity.Friction = entity?Friction () : single
        static member setFriction (value : single) (entity : Entity) : Entity = entity?Friction <- value
        member entity.Restitution = entity?Restitution () : single
        static member setRestitution (value : single) (entity : Entity) : Entity = entity?Restitution <- value
        member entity.FixedRotation = entity?FixedRotation () : bool
        static member setFixedRotation (value : bool) (entity : Entity) : Entity = entity?FixedRotation <- value
        member entity.LinearDamping = entity?LinearDamping () : single
        static member setLinearDamping (value : single) (entity : Entity) : Entity = entity?LinearDamping <- value
        member entity.AngularDamping = entity?AngularDamping () : single
        static member setAngularDamping (value : single) (entity : Entity) : Entity = entity?AngularDamping <- value
        member entity.GravityScale = entity?GravityScale () : single
        static member setGravityScale (value : single) (entity : Entity) : Entity = entity?GravityScale <- value
        member entity.CollisionCategories = entity?CollisionCategories () : string
        static member setCollisionCategories (value : string) (entity : Entity) : Entity = entity?CollisionCategories <- value
        member entity.CollisionMask = entity?CollisionMask () : string
        static member setCollisionMask (value : string) (entity : Entity) : Entity = entity?CollisionMask <- value
        member entity.IsBullet = entity?IsBullet () : bool
        static member setIsBullet (value : bool) (entity : Entity) : Entity = entity?IsBullet <- value
        member entity.IsSensor = entity?IsSensor () : bool
        static member setIsSensor (value : bool) (entity : Entity) : Entity = entity?IsSensor <- value

        static member getPhysicsId (entity : Entity) =
            PhysicsId (entity.Id, entity.MinorId)

[<RequireQualifiedAccess>]
module SimpleBodyFacet =

    let [<Literal>] Name = "SimpleBodyFacet"

    let init (entity : Entity) (_ : IXDispatcherContainer) =
        entity |>
            Entity.setMinorId (NuCore.makeId ()) |>
            Entity.setBodyType BodyType.Dynamic |>
            Entity.setDensity NormalDensity |>
            Entity.setFriction 0.0f |>
            Entity.setRestitution 0.0f |>
            Entity.setFixedRotation false |>
            Entity.setLinearDamping 1.0f |>
            Entity.setAngularDamping 1.0f |>
            Entity.setGravityScale 1.0f |>
            Entity.setCollisionCategories "1" |>
            Entity.setCollisionMask "*" |>
            Entity.setIsBullet false |>
            Entity.setIsSensor false

    let registerPhysics getBodyShape address world =
        let entity = World.getEntity address world
        let bodyProperties = 
            { Shape = getBodyShape entity world
              BodyType = entity.BodyType
              Density = entity.Density
              Friction = entity.Friction
              Restitution = entity.Restitution
              FixedRotation = entity.FixedRotation
              LinearDamping = entity.LinearDamping
              AngularDamping = entity.AngularDamping
              GravityScale = entity.GravityScale
              CollisionCategories = Physics.toCollisionCategories entity.CollisionCategories
              CollisionMask = Physics.toCollisionCategories entity.CollisionMask
              IsBullet = entity.IsBullet
              IsSensor = entity.IsSensor }
        let physicsId = Entity.getPhysicsId entity
        let position = entity.Position + entity.Size * 0.5f
        let rotation = entity.Rotation
        World.createBody address physicsId position rotation bodyProperties world

    let unregisterPhysics address world =
        let entity = World.getEntity address world
        World.destroyBody (Entity.getPhysicsId entity) world

    let propagatePhysics getBodyShape address world =
        let world = unregisterPhysics address world
        registerPhysics getBodyShape address world

    let handleBodyTransformMessage address (message : BodyTransformMessage) world =
        // OPTIMIZATION: entity is not changed (avoiding a change entity event) if position and rotation haven't changed.
        let entity = World.getEntity address world
        if entity.Position <> message.Position || entity.Rotation <> message.Rotation then
            let entity =
                entity |>
                    // TODO: see if the following center-offsetting can be encapsulated within the Physics module!
                    Entity.setPosition (message.Position - entity.Size * 0.5f) |>
                    Entity.setRotation message.Rotation
            World.setEntity message.EntityAddress entity world
        else world

[<AutoOpen>]
module SimpleSpriteFacetModule =

    type Entity with

        member entity.SpriteImage = entity?SpriteImage () : Image
        static member setSpriteImage (value : Image) (entity : Entity) : Entity = entity?SpriteImage <- value

[<RequireQualifiedAccess>]
module SimpleSpriteFacet =

    let [<Literal>] Name = "SimpleSpriteFacet"

    let init entity (_ : IXDispatcherContainer) =
        Entity.setSpriteImage { ImageAssetName = "Image3"; PackageName = DefaultPackageName } entity

    let getRenderDescriptors (entity : Entity) viewType world =
        if entity.Visible && Camera.inView3 entity.Position entity.Size world.Camera then
            [LayerableDescriptor
                { Depth = entity.Depth
                  LayeredDescriptor =
                    SpriteDescriptor
                        { Position = entity.Position
                          Size = entity.Size
                          Rotation = entity.Rotation
                          ViewType = viewType
                          OptInset = None
                          Image = entity.SpriteImage
                          Color = Vector4.One }}]
        else []

    let getQuickSize (entity : Entity) world =
        let image = entity.SpriteImage
        match Metadata.tryGetTextureSizeAsVector2 image.ImageAssetName image.PackageName world.AssetMetadataMap with
        | None -> DefaultEntitySize
        | Some size -> size

[<AutoOpen>]
module SimpleAnimatedSpriteFacetModule =

    type Entity with

        member entity.Stutter = entity?Stutter () : int
        static member setStutter (value : int) (entity : Entity) : Entity = entity?Stutter <- value
        member entity.TileCount = entity?TileCount () : int
        static member setTileCount (value : int) (entity : Entity) : Entity = entity?TileCount <- value
        member entity.TileRun = entity?TileRun () : int
        static member setTileRun (value : int) (entity : Entity) : Entity = entity?TileRun <- value
        member entity.TileSize = entity?TileSize () : Vector2
        static member setTileSize (value : Vector2) (entity : Entity) : Entity = entity?TileSize <- value

[<RequireQualifiedAccess>]
module SimpleAnimatedSpriteFacet =

    let [<Literal>] Name = "SimpleAnimatedSpriteFacet"

    let private getSpriteOptInset (entity : Entity) world =
        let tile = (int world.TickTime / entity.Stutter) % entity.TileCount
        let tileI = tile % entity.TileRun
        let tileJ = tile / entity.TileRun
        let tileX = single tileI * entity.TileSize.X
        let tileY = single tileJ * entity.TileSize.Y
        let inset = Vector4 (tileX, tileY, tileX + entity.TileSize.X, tileY + entity.TileSize.Y)
        Some inset

    let init (entity : Entity) (_ : IXDispatcherContainer) =
        entity |>
            Entity.setStutter 4 |>
            Entity.setTileCount 16 |>
            Entity.setTileRun 4 |>
            Entity.setTileSize (Vector2 (16.0f, 16.0f)) |>
            Entity.setSpriteImage { ImageAssetName = "Image7"; PackageName = DefaultPackageName }

    let getRenderDescriptors (entity : Entity) viewType world =
        if entity.Visible && Camera.inView3 entity.Position entity.Size world.Camera then
            [LayerableDescriptor
                { Depth = entity.Depth
                  LayeredDescriptor =
                    SpriteDescriptor
                        { Position = entity.Position
                          Size = entity.Size
                          Rotation = entity.Rotation
                          ViewType = viewType
                          OptInset = getSpriteOptInset entity world
                          Image = entity.SpriteImage
                          Color = Vector4.One }}]
        else []

    let getQuickSize (entity : Entity) (_ : World) =
        entity.TileSize

[<AutoOpen>]
module GuiDispatcherModule =

    type Entity with
        
        member entity.Enabled = entity?Enabled () : bool
        static member setEnabled (value : bool) (entity : Entity) : Entity = entity?Enabled <- value

    type [<AbstractClass>] GuiDispatcher (facetNames) =
        inherit EntityDispatcher (facetNames)

        override dispatcher.Init (entity, dispatcherContainer) =
            let entity = base.Init (entity, dispatcherContainer)
            Entity.setEnabled true entity

[<AutoOpen>]
module SimpleBodyDispatcherModule =

    type [<AbstractClass>] SimpleBodyDispatcher (facetNames) =
        inherit EntityDispatcher (Set.add SimpleBodyFacet.Name facetNames)

        abstract member GetBodyShape : Entity * World -> BodyShape
        default dispatcher.GetBodyShape (entity, _) =
            BoxShape { Extent = entity.Size * 0.5f; Center = Vector2.Zero }

        override dispatcher.Init (entity, dispatcherContainer) =
            let entity = base.Init (entity, dispatcherContainer)
            SimpleBodyFacet.init entity dispatcherContainer

        override dispatcher.Register (address, world) =
            let getBodyShape = (fun entity world -> dispatcher.GetBodyShape (entity, world))
            SimpleBodyFacet.registerPhysics getBodyShape address world

        override dispatcher.Unregister (address, world) =
            SimpleBodyFacet.unregisterPhysics address world
            
        override dispatcher.PropagatePhysics (address, world) =
            let getBodyShape = (fun entity world -> dispatcher.GetBodyShape (entity, world))
            SimpleBodyFacet.propagatePhysics getBodyShape address world

        override dispatcher.HandleBodyTransformMessage (address, message, world) =
            SimpleBodyFacet.handleBodyTransformMessage address message world

[<AutoOpen>]
module SimpleBodySpriteDispatcherModule =

    type [<AbstractClass>] SimpleBodySpriteDispatcher (facetNames) =
        inherit EntityDispatcher (Set.addMany [SimpleSpriteFacet.Name; SimpleBodyFacet.Name] facetNames)

        abstract member GetBodyShape : Entity * World -> BodyShape
        default dispatcher.GetBodyShape (entity, _) =
            CircleShape { Radius = entity.Size.X * 0.5f; Center = Vector2.Zero }

        override dispatcher.Init (entity, dispatcherContainer) =
            let entity = base.Init (entity, dispatcherContainer)
            let entity = SimpleBodyFacet.init entity dispatcherContainer
            SimpleSpriteFacet.init entity dispatcherContainer

        override dispatcher.Register (address, world) =
            let getBodyShape = (fun entity world -> dispatcher.GetBodyShape (entity, world))
            SimpleBodyFacet.registerPhysics getBodyShape address world

        override dispatcher.Unregister (address, world) =
            SimpleBodyFacet.unregisterPhysics address world
            
        override dispatcher.PropagatePhysics (address, world) =
            let getBodyShape = (fun entity world -> dispatcher.GetBodyShape (entity, world))
            SimpleBodyFacet.propagatePhysics getBodyShape address world

        override dispatcher.HandleBodyTransformMessage (address, message, world) =
            SimpleBodyFacet.handleBodyTransformMessage address message world

        override dispatcher.GetRenderDescriptors (entity, world) =
            SimpleSpriteFacet.getRenderDescriptors entity Relative world

        override dispatcher.GetQuickSize (entity, world) =
            SimpleSpriteFacet.getQuickSize entity world

[<AutoOpen>]
module SimpleBodyAnimatedSpriteDispatcherModule =

    type [<AbstractClass>] SimpleBodyAnimatedSpriteDispatcher (facetNames) =
        inherit EntityDispatcher (Set.addMany [SimpleAnimatedSpriteFacet.Name; SimpleBodyFacet.Name] facetNames)

        abstract member GetBodyShape : Entity * World -> BodyShape
        default dispatcher.GetBodyShape (entity, _) =
            CircleShape { Radius = entity.Size.X * 0.5f; Center = Vector2.Zero }

        override dispatcher.Init (entity, dispatcherContainer) =
            let entity = base.Init (entity, dispatcherContainer)
            let entity = SimpleBodyFacet.init entity dispatcherContainer
            SimpleAnimatedSpriteFacet.init entity dispatcherContainer

        override dispatcher.Register (address, world) =
            let getBodyShape = (fun entity world -> dispatcher.GetBodyShape (entity, world))
            SimpleBodyFacet.registerPhysics getBodyShape address world

        override dispatcher.Unregister (address, world) =
            SimpleBodyFacet.unregisterPhysics address world
            
        override dispatcher.PropagatePhysics (address, world) =
            let getBodyShape = (fun entity world -> dispatcher.GetBodyShape (entity, world))
            SimpleBodyFacet.propagatePhysics getBodyShape address world

        override dispatcher.HandleBodyTransformMessage (address, message, world) =
            SimpleBodyFacet.handleBodyTransformMessage address message world

        override dispatcher.GetRenderDescriptors (entity, world) =
            SimpleAnimatedSpriteFacet.getRenderDescriptors entity Relative world

        override dispatcher.GetQuickSize (entity, world) =
            SimpleAnimatedSpriteFacet.getQuickSize entity world

[<AutoOpen>]
module ButtonDispatcherModule =

    type Entity with

        member entity.IsDown = entity?IsDown () : bool
        static member setIsDown (value : bool) (entity : Entity) : Entity = entity?IsDown <- value
        member entity.UpImage = entity?UpImage () : Image
        static member setUpImage (value : Image) (entity : Entity) : Entity = entity?UpImage <- value
        member entity.DownImage = entity?DownImage () : Image
        static member setDownImage (value : Image) (entity : Entity) : Entity = entity?DownImage <- value
        member entity.ClickSound = entity?ClickSound () : Sound
        static member setClickSound (value : Sound) (entity : Entity) : Entity = entity?ClickSound <- value

    type [<Sealed>] ButtonDispatcher () =
        inherit GuiDispatcher (Set.empty)

        let handleButtonEventDownMouseLeft event world =
            if World.isAddressSelected event.Subscriber world then
                let mouseButtonData = EventData.toMouseButtonData event.Data
                let button = World.getEntity event.Subscriber world
                let mousePositionButton = Entity.mouseToEntity mouseButtonData.Position world button
                if button.Enabled && button.Visible then
                    if NuMath.isPointInBounds3 mousePositionButton button.Position button.Size then
                        let button = Entity.setIsDown true button
                        let world = World.setEntity event.Subscriber button world
                        let world = World.publish4 (DownEventName + event.Subscriber) event.Subscriber NoData world
                        (Handled, world)
                    else (Unhandled, world)
                else (Unhandled, world)
            else (Unhandled, world)

        let handleButtonEventUpMouseLeft event world =
            if World.isAddressSelected event.Subscriber world then
                let mouseButtonData = EventData.toMouseButtonData event.Data
                let button = World.getEntity event.Subscriber world
                let mousePositionButton = Entity.mouseToEntity mouseButtonData.Position world button
                if button.Enabled && button.Visible then
                    let world =
                        let button = Entity.setIsDown false button
                        let world = World.setEntity event.Subscriber button world
                        World.publish4 (UpEventName + event.Subscriber) event.Subscriber NoData world
                    if NuMath.isPointInBounds3 mousePositionButton button.Position button.Size && button.IsDown then
                        let world = World.publish4 (ClickEventName + event.Subscriber) event.Subscriber NoData world
                        let world = World.playSound button.ClickSound 1.0f world
                        (Handled, world)
                    else (Unhandled, world)
                else (Unhandled, world)
            else (Unhandled, world)

        override dispatcher.Init (button, dispatcherContainer) =
            let button = base.Init (button, dispatcherContainer)
            button |>
                Entity.setIsDown false |>
                Entity.setUpImage { ImageAssetName = "Image"; PackageName = DefaultPackageName } |>
                Entity.setDownImage { ImageAssetName = "Image2"; PackageName = DefaultPackageName } |>
                Entity.setClickSound { SoundAssetName = "Sound"; PackageName = DefaultPackageName }

        override dispatcher.Register (address, world) =
            world |>
                World.observe DownMouseLeftEventName address (CustomSub handleButtonEventDownMouseLeft) |>
                World.observe UpMouseLeftEventName address (CustomSub handleButtonEventUpMouseLeft)

        override dispatcher.GetRenderDescriptors (button, _) =
            if button.Visible then
                [LayerableDescriptor
                    { Depth = button.Depth
                      LayeredDescriptor =
                        SpriteDescriptor
                            { Position = button.Position
                              Size = button.Size
                              Rotation = 0.0f
                              ViewType = Absolute
                              OptInset = None
                              Image = if button.IsDown then button.DownImage else button.UpImage
                              Color = Vector4.One }}]
            else []

        override dispatcher.GetQuickSize (button, world) =
            let image = button.UpImage
            match Metadata.tryGetTextureSizeAsVector2 image.ImageAssetName image.PackageName world.AssetMetadataMap with
            | None -> DefaultEntitySize
            | Some size -> size

        override dispatcher.IsTransformRelative (_, _) =
            false

[<AutoOpen>]
module LabelDispatcherModule =

    type Entity with

        member entity.LabelImage = entity?LabelImage () : Image
        static member setLabelImage (value : Image) (entity : Entity) : Entity = entity?LabelImage <- value

    type [<Sealed>] LabelDispatcher () =
        inherit GuiDispatcher (Set.empty)
            
        override dispatcher.Init (label, dispatcherContainer) =
            let label = base.Init (label, dispatcherContainer)
            Entity.setLabelImage { ImageAssetName = "Image4"; PackageName = DefaultPackageName } label

        override dispatcher.GetRenderDescriptors (label, _) =
            if label.Visible then
                [LayerableDescriptor
                    { Depth = label.Depth
                      LayeredDescriptor =
                        SpriteDescriptor
                            { Position = label.Position
                              Size = label.Size
                              Rotation = 0.0f
                              ViewType = Absolute
                              OptInset = None
                              Image = label.LabelImage
                              Color = Vector4.One }}]
            else []

        override dispatcher.GetQuickSize (label, world) =
            let image = label.LabelImage
            match Metadata.tryGetTextureSizeAsVector2 image.ImageAssetName image.PackageName world.AssetMetadataMap with
            | None -> DefaultEntitySize
            | Some size -> size

        override dispatcher.IsTransformRelative (_, _) =
            false

[<AutoOpen>]
module TextDispatcherModule =

    type Entity with

        member entity.BackgroundImage = entity?BackgroundImage () : Image
        static member setBackgroundImage (value : Image) (entity : Entity) : Entity = entity?BackgroundImage <- value
        member entity.Text = entity?Text () : string
        static member setText (value : string) (entity : Entity) : Entity = entity?Text <- value
        member entity.TextFont = entity?TextFont () : Font
        static member setTextFont (value : Font) (entity : Entity) : Entity = entity?TextFont <- value
        member entity.TextOffset = entity?TextOffset () : Vector2
        static member setTextOffset (value : Vector2) (entity : Entity) : Entity = entity?TextOffset <- value
        member entity.TextColor = entity?TextColor () : Vector4
        static member setTextColor (value : Vector4) (entity : Entity) : Entity = entity?TextColor <- value

    type [<Sealed>] TextDispatcher () =
        inherit GuiDispatcher (Set.empty)

        override dispatcher.Init (text, dispatcherContainer) =
            let text = base.Init (text, dispatcherContainer)
            text |>
                Entity.setBackgroundImage { ImageAssetName = "Image4"; PackageName = DefaultPackageName } |>
                Entity.setText String.Empty |>
                Entity.setTextFont { FontAssetName = "Font"; PackageName = DefaultPackageName } |>
                Entity.setTextOffset Vector2.Zero |>
                Entity.setTextColor Vector4.One

        override dispatcher.GetRenderDescriptors (text, _) =
            if text.Visible then
                [LayerableDescriptor
                    { Depth = text.Depth
                      LayeredDescriptor =
                        SpriteDescriptor
                            { Position = text.Position
                              Size = text.Size
                              Rotation = 0.0f
                              ViewType = Absolute
                              OptInset = None
                              Image = text.BackgroundImage
                              Color = Vector4.One }}
                 LayerableDescriptor
                    { Depth = text.Depth
                      LayeredDescriptor =
                        TextDescriptor
                            { Text = text.Text
                              Position = (text.Position + text.TextOffset)
                              Size = text.Size - text.TextOffset
                              ViewType = Absolute
                              Font = text.TextFont
                              Color = text.TextColor }}]
            else []

        override dispatcher.GetQuickSize (text, world) =
            let image = text.BackgroundImage
            match Metadata.tryGetTextureSizeAsVector2 image.ImageAssetName image.PackageName world.AssetMetadataMap with
            | None -> DefaultEntitySize
            | Some size -> size

        override dispatcher.IsTransformRelative (_, _) =
            false

[<AutoOpen>]
module ToggleDispatcherModule =

    type Entity with

        member entity.IsOn = entity?IsOn () : bool
        static member setIsOn (value : bool) (entity : Entity) : Entity = entity?IsOn <- value
        member entity.IsPressed = entity?IsPressed () : bool
        static member setIsPressed (value : bool) (entity : Entity) : Entity = entity?IsPressed <- value
        member entity.OffImage = entity?OffImage () : Image
        static member setOffImage (value : Image) (entity : Entity) : Entity = entity?OffImage <- value
        member entity.OnImage = entity?OnImage () : Image
        static member setOnImage (value : Image) (entity : Entity) : Entity = entity?OnImage <- value
        member entity.ToggleSound = entity?ToggleSound () : Sound
        static member setToggleSound (value : Sound) (entity : Entity) : Entity = entity?ToggleSound <- value

    type [<Sealed>] ToggleDispatcher () =
        inherit GuiDispatcher (Set.empty)

        let handleToggleEventDownMouseLeft event world =
            if World.isAddressSelected event.Subscriber world then
                let mouseButtonData = EventData.toMouseButtonData event.Data
                let toggle = World.getEntity event.Subscriber world
                let mousePositionToggle = Entity.mouseToEntity mouseButtonData.Position world toggle
                if toggle.Enabled && toggle.Visible then
                    if NuMath.isPointInBounds3 mousePositionToggle toggle.Position toggle.Size then
                        let toggle = Entity.setIsPressed true toggle
                        let world = World.setEntity event.Subscriber toggle world
                        (Handled, world)
                    else (Unhandled, world)
                else (Unhandled, world)
            else (Unhandled, world)
    
        let handleToggleEventUpMouseLeft event world =
            if World.isAddressSelected event.Subscriber world then
                let mouseButtonData = EventData.toMouseButtonData event.Data
                let toggle = World.getEntity event.Subscriber world
                let mousePositionToggle = Entity.mouseToEntity mouseButtonData.Position world toggle
                if toggle.Enabled && toggle.Visible && toggle.IsPressed then
                    let toggle = Entity.setIsPressed false toggle
                    if NuMath.isPointInBounds3 mousePositionToggle toggle.Position toggle.Size then
                        let toggle = Entity.setIsOn (not toggle.IsOn) toggle
                        let world = World.setEntity event.Subscriber toggle world
                        let eventName = if toggle.IsOn then OnEventName else OffEventName
                        let world = World.publish4 (eventName + event.Subscriber) event.Subscriber NoData world
                        let world = World.playSound toggle.ToggleSound 1.0f world
                        (Handled, world)
                    else
                        let world = World.setEntity event.Subscriber toggle world
                        (Unhandled, world)
                else (Unhandled, world)
            else (Unhandled, world)
        
        override dispatcher.Init (toggle, dispatcherContainer) =
            let toggle = base.Init (toggle, dispatcherContainer)
            toggle |>
                Entity.setIsOn false |>
                Entity.setIsPressed false |>
                Entity.setOffImage { ImageAssetName = "Image"; PackageName = DefaultPackageName } |>
                Entity.setOnImage { ImageAssetName = "Image2"; PackageName = DefaultPackageName } |>
                Entity.setToggleSound { SoundAssetName = "Sound"; PackageName = DefaultPackageName }

        override dispatcher.Register (address, world) =
            world |>
                World.observe DownMouseLeftEventName address (CustomSub handleToggleEventDownMouseLeft) |>
                World.observe UpMouseLeftEventName address (CustomSub handleToggleEventUpMouseLeft)

        override dispatcher.GetRenderDescriptors (toggle, _) =
            if toggle.Visible then
                [LayerableDescriptor
                    { Depth = toggle.Depth
                      LayeredDescriptor =
                        SpriteDescriptor
                            { Position = toggle.Position
                              Size = toggle.Size
                              Rotation = 0.0f
                              ViewType = Absolute
                              OptInset = None
                              Image = if toggle.IsOn || toggle.IsPressed then toggle.OnImage else toggle.OffImage
                              Color = Vector4.One }}]
            else []

        override dispatcher.GetQuickSize (toggle, world) =
            let image = toggle.OffImage
            match Metadata.tryGetTextureSizeAsVector2 image.ImageAssetName image.PackageName world.AssetMetadataMap with
            | None -> DefaultEntitySize
            | Some size -> size

        override dispatcher.IsTransformRelative (_, _) =
            false

[<AutoOpen>]
module FeelerDispatcherModule =

    type Entity with

        member entity.IsTouched = entity?IsTouched () : bool
        static member setIsTouched (value : bool) (entity : Entity) : Entity = entity?IsTouched <- value

    type [<Sealed>] FeelerDispatcher () =
        inherit GuiDispatcher (Set.empty)

        let handleFeelerEventDownMouseLeft event world =
            if World.isAddressSelected event.Subscriber world then
                let mouseButtonData = EventData.toMouseButtonData event.Data
                let feeler = World.getEntity event.Subscriber world
                let mousePositionFeeler = Entity.mouseToEntity mouseButtonData.Position world feeler
                if feeler.Enabled && feeler.Visible then
                    if NuMath.isPointInBounds3 mousePositionFeeler feeler.Position feeler.Size then
                        let feeler = Entity.setIsTouched true feeler
                        let world = World.setEntity event.Subscriber feeler world
                        let world = World.publish4 (TouchEventName + event.Subscriber) event.Subscriber (MouseButtonData mouseButtonData) world
                        (Handled, world)
                    else (Unhandled, world)
                else (Unhandled, world)
            else (Unhandled, world)
    
        let handleFeelerEventUpMouseLeft event world =
            if World.isAddressSelected event.Subscriber world then
                let feeler = World.getEntity event.Subscriber world
                if feeler.Enabled && feeler.Visible then
                    let feeler = Entity.setIsTouched false feeler
                    let world = World.setEntity event.Subscriber feeler world
                    let world = World.publish4 (ReleaseEventName + event.Subscriber) event.Subscriber NoData world
                    (Handled, world)
                else (Unhandled, world)
            else (Unhandled, world)
        
        override dispatcher.Init (feeler, dispatcherContainer) =
            let feeler = base.Init (feeler, dispatcherContainer)
            Entity.setIsTouched false feeler

        override dispatcher.Register (address, world) =
            world |>
                World.observe DownMouseLeftEventName address (CustomSub handleFeelerEventDownMouseLeft) |>
                World.observe UpMouseLeftEventName address (CustomSub handleFeelerEventUpMouseLeft)

        override dispatcher.GetQuickSize (_, _) =
            Vector2 64.0f

        override dispatcher.IsTransformRelative (_, _) =
            false

[<AutoOpen>]
module FillBarDispatcherModule =

    type Entity with
    
        member entity.Fill = entity?Fill () : single
        static member setFill (value : single) (entity : Entity) : Entity = entity?Fill <- value
        member entity.FillInset = entity?FillInset () : single
        static member setFillInset (value : single) (entity : Entity) : Entity = entity?FillInset <- value
        member entity.FillImage = entity?FillImage () : Image
        static member setFillImage (value : Image) (entity : Entity) : Entity = entity?FillImage <- value
        member entity.BorderImage = entity?BorderImage () : Image
        static member setBorderImage (value : Image) (entity : Entity) : Entity = entity?BorderImage <- value

    type [<Sealed>] FillBarDispatcher () =
        inherit GuiDispatcher (Set.empty)

        let getFillBarSpriteDims (fillBar : Entity) =
            let spriteInset = fillBar.Size * fillBar.FillInset * 0.5f
            let spritePosition = fillBar.Position + spriteInset
            let spriteWidth = (fillBar.Size.X - spriteInset.X * 2.0f) * fillBar.Fill
            let spriteHeight = fillBar.Size.Y - spriteInset.Y * 2.0f
            (spritePosition, Vector2 (spriteWidth, spriteHeight))

        override dispatcher.Init (fillBar, dispatcherContainer) =
            let fillBar = base.Init (fillBar, dispatcherContainer)
            fillBar |>
                Entity.setFill 0.0f |>
                Entity.setFillInset 0.0f |>
                Entity.setFillImage { ImageAssetName = "Image9"; PackageName = DefaultPackageName } |>
                Entity.setBorderImage { ImageAssetName = "Image10"; PackageName = DefaultPackageName }
                
        override dispatcher.GetRenderDescriptors (fillBar, _) =
            if fillBar.Visible then
                let (fillBarSpritePosition, fillBarSpriteSize) = getFillBarSpriteDims fillBar
                [LayerableDescriptor
                    { Depth = fillBar.Depth
                      LayeredDescriptor =
                        SpriteDescriptor
                            { Position = fillBarSpritePosition
                              Size = fillBarSpriteSize
                              Rotation = 0.0f
                              ViewType = Absolute
                              OptInset = None
                              Image = fillBar.FillImage
                              Color = Vector4.One }}
                 LayerableDescriptor
                    { Depth = fillBar.Depth
                      LayeredDescriptor =
                        SpriteDescriptor
                            { Position = fillBar.Position
                              Size = fillBar.Size
                              Rotation = 0.0f
                              ViewType = Absolute
                              OptInset = None
                              Image = fillBar.BorderImage
                              Color = Vector4.One }}]
            else []

        override dispatcher.GetQuickSize (fillBar, world) =
            let image = fillBar.BorderImage
            match Metadata.tryGetTextureSizeAsVector2 image.ImageAssetName image.PackageName world.AssetMetadataMap with
            | None -> DefaultEntitySize
            | Some size -> size

        override dispatcher.IsTransformRelative (_, _) =
            false

[<AutoOpen>]
module BlockDispatcherModule =

    type [<Sealed>] BlockDispatcher () =
        inherit SimpleBodySpriteDispatcher (Set.empty)

        override dispatcher.Init (block, dispatcherContainer) =
            let block = base.Init (block, dispatcherContainer)
            Entity.setSpriteImage { ImageAssetName = "Image3"; PackageName = DefaultPackageName } block

        override dispatcher.GetBodyShape (block, _) =
            BoxShape { Extent = block.Size * 0.5f; Center = Vector2.Zero }

[<AutoOpen>]
module AvatarDispatcherModule =

    type [<Sealed>] AvatarDispatcher () =
        inherit SimpleBodySpriteDispatcher (Set.empty)

        override dispatcher.Init (avatar, dispatcherContainer) =
            let avatar = base.Init (avatar, dispatcherContainer)
            avatar |>
                Entity.setFixedRotation true |>
                Entity.setLinearDamping 10.0f |>
                Entity.setGravityScale 0.0f |>
                Entity.setSpriteImage { ImageAssetName = "Image7"; PackageName = DefaultPackageName }

        override dispatcher.GetBodyShape (avatar, _) =
            CircleShape { Radius = avatar.Size.X * 0.5f; Center = Vector2.Zero }

[<AutoOpen>]
module CharacterDispatcherModule =

    type [<Sealed>] CharacterDispatcher () =
        inherit SimpleBodySpriteDispatcher (Set.empty)

        override dispatcher.Init (character, dispatcherContainer) =
            let character = base.Init (character, dispatcherContainer)
            character |>
                Entity.setFixedRotation true |>
                Entity.setLinearDamping 3.0f |>
                Entity.setSpriteImage { ImageAssetName = "Image6"; PackageName = DefaultPackageName }

        override dispatcher.GetBodyShape (character, _) =
            CapsuleShape { Height = character.Size.Y * 0.5f; Radius = character.Size.Y * 0.25f; Center = Vector2.Zero }

[<AutoOpen>]
module TileMapDispatcherModule =

    type Entity with

        member entity.TileMapAsset = entity?TileMapAsset () : TileMapAsset
        static member setTileMapAsset (value : TileMapAsset) (entity : Entity) : Entity = entity?TileMapAsset <- value
        member entity.Parallax = entity?Parallax () : single
        static member setParallax (value : single) (entity : Entity) : Entity = entity?Parallax <- value

        static member makeTileMapData tileMapAsset world =
            let (_, _, map) = Metadata.getTileMapMetadata tileMapAsset.TileMapAssetName tileMapAsset.PackageName world.AssetMetadataMap
            let mapSize = (map.Width, map.Height)
            let tileSize = (map.TileWidth, map.TileHeight)
            let tileSizeF = Vector2 (single <| fst tileSize, single <| snd tileSize)
            let tileMapSize = (fst mapSize * fst tileSize, snd mapSize * snd tileSize)
            let tileMapSizeF = Vector2 (single <| fst tileMapSize, single <| snd tileMapSize)
            let tileSet = map.Tilesets.[0] // MAGIC_VALUE: I'm not sure how to properly specify this
            let optTileSetWidth = tileSet.Image.Width
            let optTileSetHeight = tileSet.Image.Height
            let tileSetSize = (optTileSetWidth.Value / fst tileSize, optTileSetHeight.Value / snd tileSize)
            { Map = map; MapSize = mapSize; TileSize = tileSize; TileSizeF = tileSizeF; TileMapSize = tileMapSize; TileMapSizeF = tileMapSizeF; TileSet = tileSet; TileSetSize = tileSetSize }

        static member makeTileData (tileMap : Entity) tmd (tl : TmxLayer) tileIndex =
            let mapRun = fst tmd.MapSize
            let tileSetRun = fst tmd.TileSetSize
            let (i, j) = (tileIndex % mapRun, tileIndex / mapRun)
            let tile = tl.Tiles.[tileIndex]
            let gid = tile.Gid - tmd.TileSet.FirstGid
            let gidPosition = gid * fst tmd.TileSize
            let gid2 = (gid % tileSetRun, gid / tileSetRun)
            let tileMapPosition = tileMap.Position
            let tilePosition = (
                int tileMapPosition.X + fst tmd.TileSize * i,
                int tileMapPosition.Y - snd tmd.TileSize * (j + 1)) // subtraction for right-handedness
            let optTileSetTile = Seq.tryFind (fun (item : TmxTilesetTile) -> tile.Gid - 1 = item.Id) tmd.TileSet.Tiles
            { Tile = tile; I = i; J = j; Gid = gid; GidPosition = gidPosition; Gid2 = gid2; TilePosition = tilePosition; OptTileSetTile = optTileSetTile }

    type [<Sealed>] TileMapDispatcher () =
        inherit EntityDispatcher (Set.empty)

        let getTilePhysicsId tmid (tli : int) (ti : int) =
            PhysicsId (tmid, intsToGuid tli ti)

        let registerTilePhysicsBox address (tm : Entity) tmd tli td ti world =
            let physicsId = getTilePhysicsId tm.Id tli ti
            let createBodyMessage =
                CreateBodyMessage
                    { EntityAddress = address
                      PhysicsId = physicsId
                      Position =
                        Vector2 (
                            single <| fst td.TilePosition + fst tmd.TileSize / 2,
                            single <| snd td.TilePosition + snd tmd.TileSize / 2 + snd tmd.TileMapSize)
                      Rotation = tm.Rotation
                      BodyProperties =
                        { Shape = BoxShape { Extent = Vector2 (single <| fst tmd.TileSize, single <| snd tmd.TileSize) * 0.5f; Center = Vector2.Zero }
                          BodyType = BodyType.Static
                          Density = tm.Density
                          Friction = tm.Friction
                          Restitution = tm.Restitution
                          FixedRotation = true
                          LinearDamping = 0.0f
                          AngularDamping = 0.0f
                          GravityScale = 0.0f
                          CollisionCategories = Physics.toCollisionCategories tm.CollisionCategories
                          CollisionMask = Physics.toCollisionCategories tm.CollisionMask
                          IsBullet = false
                          IsSensor = false }}
            { world with PhysicsMessages = createBodyMessage :: world.PhysicsMessages }

        let registerTilePhysicsPolygon address (tm : Entity) tmd tli td ti vertices world =
            let physicsId = getTilePhysicsId tm.Id tli ti
            let createBodyMessage =
                CreateBodyMessage
                    { EntityAddress = address
                      PhysicsId = physicsId
                      Position =
                        Vector2 (
                            single <| fst td.TilePosition + fst tmd.TileSize / 2,
                            single <| snd td.TilePosition + snd tmd.TileSize / 2 + snd tmd.TileMapSize)
                      Rotation = tm.Rotation
                      BodyProperties =
                        { Shape = PolygonShape { Vertices = vertices; Center = Vector2.Zero }
                          BodyType = BodyType.Static
                          Density = tm.Density
                          Friction = tm.Friction
                          Restitution = tm.Restitution
                          FixedRotation = true
                          LinearDamping = 0.0f
                          AngularDamping = 0.0f
                          GravityScale = 0.0f
                          CollisionCategories = Physics.toCollisionCategories tm.CollisionCategories
                          CollisionMask = Physics.toCollisionCategories tm.CollisionMask
                          IsBullet = false
                          IsSensor = false }}
            { world with PhysicsMessages = createBodyMessage :: world.PhysicsMessages }

        let registerTilePhysics tm tmd (tl : TmxLayer) tli address ti world _ =
            let td = Entity.makeTileData tm tmd tl ti
            match td.OptTileSetTile with
            | None -> world
            | Some tileSetTile ->
                let collisionProperty = ref Unchecked.defaultof<string>
                if tileSetTile.Properties.TryGetValue (CollisionProperty, collisionProperty) then
                    let collisionExpr = string collisionProperty.Value
                    let collisionTerms = List.ofArray <| collisionExpr.Split '?'
                    let collisionTermsTrimmed = List.map (fun (term : string) -> term.Trim ()) collisionTerms
                    match collisionTermsTrimmed with
                    | [""]
                    | ["Box"] -> registerTilePhysicsBox address tm tmd tli td ti world
                    | ["Polygon"; verticesStr] ->
                        let vertexStrs = List.ofArray <| verticesStr.Split '|'
                        try let vertices = List.map (fun str -> (TypeDescriptor.GetConverter (typeof<Vector2>)).ConvertFromString str :?> Vector2) vertexStrs
                            let verticesOffset = List.map (fun vertex -> vertex - Vector2 0.5f) vertices
                            let verticesScaled = List.map (fun vertex -> Vector2.Multiply (vertex, tmd.TileSizeF)) verticesOffset
                            registerTilePhysicsPolygon address tm tmd tli td ti verticesScaled world
                        with :? NotSupportedException ->
                            trace <| "Could not parse collision polygon vertices '" + verticesStr + "'. Format is 'Polygon ? 0.0;0.0 | 0.0;1.0 | 1.0;1.0 | 1.0;0.0'"
                            world
                    | _ ->
                        trace <| "Invalid tile collision shape expression '" + collisionExpr + "'."
                        world
                else world

        let registerTileLayerPhysics address tileMap tileMapData tileLayerIndex world (tileLayer : TmxLayer) =
            if tileLayer.Properties.ContainsKey CollisionProperty then
                Seq.foldi
                    (registerTilePhysics tileMap tileMapData tileLayer tileLayerIndex address)
                    world
                    tileLayer.Tiles
            else world

        let registerTileMapPhysics address world =
            let tileMap = World.getEntity address world
            let tileMapData = Entity.makeTileMapData tileMap.TileMapAsset world
            Seq.foldi
                (registerTileLayerPhysics address tileMap tileMapData)
                world
                tileMapData.Map.Layers

        let unregisterTileMapPhysics address world =
            let tileMap = World.getEntity address world
            let tileMapData = Entity.makeTileMapData tileMap.TileMapAsset world
            Seq.foldi
                (fun tileLayerIndex world (tileLayer : TmxLayer) ->
                    if tileLayer.Properties.ContainsKey CollisionProperty then
                        Seq.foldi
                            (fun tileIndex world _ ->
                                let tileData = Entity.makeTileData tileMap tileMapData tileLayer tileIndex
                                match tileData.OptTileSetTile with
                                | None -> world
                                | Some tileSetTile ->
                                    if tileSetTile.Properties.ContainsKey CollisionProperty then
                                        let physicsId = getTilePhysicsId tileMap.Id tileLayerIndex tileIndex
                                        let destroyBodyMessage = DestroyBodyMessage { PhysicsId = physicsId }
                                        { world with PhysicsMessages = destroyBodyMessage :: world.PhysicsMessages }
                                    else world)
                            world
                            tileLayer.Tiles
                    else world)
                world
                tileMapData.Map.Layers
        
        override dispatcher.Init (tileMap, dispatcherContainer) =
            let tileMap = base.Init (tileMap, dispatcherContainer)
            tileMap |>
                Entity.setDensity NormalDensity |>
                Entity.setFriction 0.0f |>
                Entity.setRestitution 0.0f |>
                Entity.setCollisionCategories "1" |>
                Entity.setCollisionMask "*" |>
                Entity.setTileMapAsset { TileMapAssetName = "TileMap"; PackageName = DefaultPackageName } |>
                Entity.setParallax 0.0f

        override dispatcher.Register (address, world) =
            registerTileMapPhysics address world

        override dispatcher.Unregister (address, world) =
            unregisterTileMapPhysics address world
            
        override dispatcher.PropagatePhysics (address, world) =
            world |>
                unregisterTileMapPhysics address |>
                registerTileMapPhysics address

        override dispatcher.GetRenderDescriptors (tileMap, world) =
            if tileMap.Visible then
                let tileMapAsset = tileMap.TileMapAsset
                match Metadata.tryGetTileMapMetadata tileMapAsset.TileMapAssetName tileMapAsset.PackageName world.AssetMetadataMap with
                | None -> []
                | Some (_, images, map) ->
                    let layers = List.ofSeq map.Layers
                    let tileSourceSize = (map.TileWidth, map.TileHeight)
                    let tileSize = Vector2 (single map.TileWidth, single map.TileHeight)
                    List.foldi
                        (fun i descriptors (layer : TmxLayer) ->
                            let depth = tileMap.Depth + single i * 2.0f // MAGIC_VALUE: assumption
                            let parallaxTranslation = tileMap.Parallax * depth * -world.Camera.EyeCenter
                            let parallaxPosition = tileMap.Position + parallaxTranslation
                            let size = Vector2 (tileSize.X * single map.Width, tileSize.Y * single map.Height)
                            if Camera.inView3 parallaxPosition size world.Camera then
                                let descriptor =
                                    LayerableDescriptor 
                                        { Depth = depth
                                          LayeredDescriptor =
                                            TileLayerDescriptor
                                                { Position = parallaxPosition
                                                  Size = size
                                                  Rotation = tileMap.Rotation
                                                  ViewType = Relative
                                                  MapSize = (map.Width, map.Height)
                                                  Tiles = layer.Tiles
                                                  TileSourceSize = tileSourceSize
                                                  TileSize = tileSize
                                                  TileSet = map.Tilesets.[0] // MAGIC_VALUE: I have no idea how to tell which tile set each tile is from...
                                                  TileSetImage = List.head images }} // MAGIC_VALUE: for same reason as above
                                descriptor :: descriptors
                            else descriptors)
                        []
                        layers
            else []

        override dispatcher.GetQuickSize (tileMap, world) =
            let tileMapAsset = tileMap.TileMapAsset
            match Metadata.tryGetTileMapMetadata tileMapAsset.TileMapAssetName tileMapAsset.PackageName world.AssetMetadataMap with
            | None -> failwith "Unexpected match failure in Nu.World.TileMapDispatcher.GetQuickSize."
            | Some (_, _, map) -> Vector2 (single <| map.Width * map.TileWidth, single <| map.Height * map.TileHeight)
