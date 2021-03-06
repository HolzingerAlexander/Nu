﻿namespace InfinityRpg
open System
open Prime
open Nu

type FieldTileType =
    | Impassable
    | Passable

type [<ReferenceEquality; NoComparison>] FieldTile =
    { TileSheetCoordinates : Vector2i
      TileType : FieldTileType }

type [<ReferenceEquality; NoComparison>] FieldMap =
    { FieldSizeC : Vector2i
      FieldTiles : Map<Vector2i, FieldTile>
      FieldTileSheet : Image AssetTag }

[<RequireQualifiedAccess>]
module FieldMap =

    let PathTile = { TileSheetCoordinates = v2i 3 0; TileType = Passable }
    let GrassTile = { TileSheetCoordinates = v2i 3 3; TileType = Passable }
    let TreeTile = { TileSheetCoordinates = v2i 1 1; TileType = Impassable }
    let StoneTile = { TileSheetCoordinates = v2i 2 3; TileType = Impassable }
    let WaterTile = { TileSheetCoordinates = v2i 0 1; TileType = Impassable }

    let getTileInDirection coordinates direction =
        match direction with
        | Upward -> coordinates + v2iUp
        | Rightward -> coordinates + v2iRight
        | Downward -> coordinates + v2iDown
        | Leftward -> coordinates + v2iLeft

    let getAdjacentTiles coordinates =
        [coordinates + v2iUp; coordinates + v2iRight; coordinates + v2iDown; coordinates + v2iLeft]
    
    let tileIs coordinates tile buildBoundsC tileMap =
        MapBounds.isPointInBounds coordinates buildBoundsC && Map.find coordinates tileMap = tile
    
    let tileInDirectionIs coordinates direction tile buildBoundsC tileMap =
        let tileInDirection = getTileInDirection coordinates direction
        tileIs tileInDirection tile buildBoundsC tileMap

    let atLeastNAdjacentTilesAre n coordinates tile buildBoundsC tileMap =
        let passingTiles = getAdjacentTiles coordinates |> List.filter (fun x -> tileIs x tile buildBoundsC tileMap)
        passingTiles.Length >= n
    
    let makeGrid boundsC =
        seq {
            for i in boundsC.CornerNegative.X .. boundsC.CornerPositive.X do
                for j in boundsC.CornerNegative.Y .. boundsC.CornerPositive.Y do
                    yield v2i i j }

    let generateEmptyMap (offsetC : Vector2i) (sizeC : Vector2i) =
        Map.ofList
            [for i in offsetC.X .. offsetC.X + sizeC.X - 1 do
                for j in offsetC.Y .. offsetC.Y + sizeC.Y - 1 do
                    let tileCoordinatesC = v2i i j
                    yield (tileCoordinatesC, GrassTile)]

    let addPaths buildBoundsC pathEdgesC generatedMap rand =
        
        let (paths, rand) =
            List.fold
                (fun (paths, rand) (sourceM, destinationM) ->
                    let (path, rand) = Direction.wanderToDestination buildBoundsC sourceM destinationM rand
                    (path :: paths, rand))
                ([], rand)
                pathEdgesC

        let generatedMap =
            Seq.fold
                (fun generatedMap path ->
                    let generatedMap' =
                        Seq.fold
                            (fun generatedMap tileCoordinates -> Map.add tileCoordinates PathTile generatedMap)
                            generatedMap
                            path
                    generatedMap @@ generatedMap')
                generatedMap
                paths

        (generatedMap, rand)

    let addTrees buildBoundsC generatedMap rand =
        let grid = makeGrid buildBoundsC
        let pathTileCount = Map.filter (fun _ v -> v = PathTile) generatedMap |> Map.count
        let treeDilution =
            if pathTileCount < 25 then 32
            elif pathTileCount < 50 then 16
            elif pathTileCount < 60 then 8
            elif pathTileCount < 70 then 4
            elif pathTileCount < 80 then 3
            elif pathTileCount < 90 then 2
            else 1
        Seq.fold
            (fun (generatedMap, rand) coordinates ->
                let (n, rand) = Rand.nextIntUnder treeDilution rand // original value is 16
                if n = 0 && Map.find coordinates generatedMap <> PathTile
                then (Map.add coordinates TreeTile generatedMap, rand)
                else (generatedMap, Rand.advance rand))
            (generatedMap, rand)
            grid

    let spreadTrees buildBoundsC generatedMap rand =
        let originalMap = generatedMap
        let grid = makeGrid buildBoundsC
        Seq.fold
            (fun (generatedMap, rand) coordinates ->
                let (n, rand) = Rand.nextIntUnder 3 rand
                if n = 0 && Map.find coordinates originalMap <> PathTile && atLeastNAdjacentTilesAre 1 coordinates TreeTile buildBoundsC originalMap && MapBounds.isPointInBounds coordinates buildBoundsC
                then (Map.add coordinates TreeTile generatedMap, rand)
                else (generatedMap, Rand.advance rand))
            (generatedMap, rand)
            grid

    let addWater buildBoundsC generatedMap rand =
        let pathTileCount = Map.filter (fun _ v -> v = PathTile) generatedMap |> Map.count
        if pathTileCount < 25 then
            let grid = makeGrid buildBoundsC
            Seq.fold
                (fun (generatedMap, rand) coordinates ->
                    let (n, rand) = Rand.nextIntUnder 128 rand
                    if n = 0 && Map.find coordinates generatedMap = GrassTile && atLeastNAdjacentTilesAre 4 coordinates GrassTile buildBoundsC generatedMap
                    then (Map.add coordinates WaterTile generatedMap, rand)
                    else (generatedMap, rand))
                (generatedMap, rand)
                grid
        else (generatedMap, rand)

    let spreadWater1 buildBoundsC generatedMap rand =
        let pathTileCount = Map.filter (fun _ v -> v = PathTile) generatedMap |> Map.count
        if pathTileCount < 25 then
            let grid = makeGrid buildBoundsC
            Seq.fold
                (fun (generatedMap, rand) coordinates ->
                    let (n, rand) = Rand.nextIntUnder 2 rand
                    if n = 0 && Map.find coordinates generatedMap <> PathTile && atLeastNAdjacentTilesAre 1 coordinates WaterTile buildBoundsC generatedMap
                    then (Map.add coordinates WaterTile generatedMap, rand)
                    else (generatedMap, rand))
                (generatedMap, rand)
                grid
        else (generatedMap, rand)

    let spreadWater2 buildBoundsC generatedMap rand =
        let pathTileCount = Map.filter (fun _ v -> v = PathTile) generatedMap |> Map.count
        if pathTileCount < 25 then
            let grid = makeGrid buildBoundsC
            let originalMap = generatedMap
            Seq.fold
                (fun (generatedMap, rand) coordinates ->
                    let (n, rand) = Rand.nextIntUnder 1 rand
                    if n = 0 && Map.find coordinates generatedMap <> PathTile && atLeastNAdjacentTilesAre 1 coordinates WaterTile buildBoundsC originalMap
                    then (Map.add coordinates WaterTile generatedMap, rand)
                    else (generatedMap, rand))
                (generatedMap, rand)
                grid
        else (generatedMap, rand)
    
    let addStones buildBoundsC generatedMap rand =
        let grid = makeGrid buildBoundsC
        Seq.fold
            (fun (generatedMap, rand) coordinates ->
                if Map.find coordinates generatedMap = GrassTile && atLeastNAdjacentTilesAre 4 coordinates PathTile buildBoundsC generatedMap
                then (Map.add coordinates StoneTile generatedMap, Rand.advance rand)
                else (generatedMap, Rand.advance rand))
            (generatedMap, rand)
            grid
    
    let make tileSheet (offsetC : Vector2i) sizeC pathEdgesC rand =
        let buildBoundsC = MapBounds.make offsetC sizeC
        let generatedMap = generateEmptyMap offsetC sizeC
        let (generatedMap, rand) = addPaths buildBoundsC pathEdgesC generatedMap rand
        let (generatedMap, rand) = addTrees buildBoundsC generatedMap rand
        let (generatedMap, rand) = spreadTrees buildBoundsC generatedMap rand
        let (generatedMap, rand) = spreadTrees buildBoundsC generatedMap rand
        let (generatedMap, rand) = spreadTrees buildBoundsC generatedMap rand
        let (generatedMap, rand) = addWater buildBoundsC generatedMap rand
        let (generatedMap, rand) = spreadWater1 buildBoundsC generatedMap rand
        let (generatedMap, rand) = spreadWater1 buildBoundsC generatedMap rand
        let (generatedMap, rand) = spreadWater2 buildBoundsC generatedMap rand
        let (generatedMap, rand) = addStones buildBoundsC generatedMap rand
        let fieldMap = { FieldSizeC = sizeC; FieldTiles = generatedMap; FieldTileSheet = tileSheet }
        (fieldMap, rand)

    let makeFromMetaTile (metaTile : MetaTile) =
        let rand = Rand.makeFromSeedState metaTile.RandSeed
        make Assets.Gameplay.FieldTileSheetImage v2iZero Constants.Layout.FieldMapSizeC [(metaTile.PathStart, metaTile.PathEnd)] rand |> fst