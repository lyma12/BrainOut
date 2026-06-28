# BrainSort — Luồng chạy Runtime

## Tổng quan

```
LevelData (ScriptableObject)
    └── RequirementNodeData[]
    └── LogicGateNodeData[]
    └── RequirementConnectionData[]   (Requirement → Gate)
    └── LogicGateConnectionData[]     (Gate → Stage)
    └── ActionConnectionData[]        (Requirement → ActionNode)
    └── ActionNodeData[]
    └── TransitionData[]
```

---

## 1. Khởi động Level

```
GamePlayController.Start()
    → LoadLevel(levelData)
        → SpawnLevelPrefab()          -- Instantiate prefab vào scene
        → CollectSceneObjects()
            → tìm tất cả ActionLinker trong scene → _actionLinkers[nodeID]
            → tìm tất cả RequirementLinker trong scene → _requirementLinkers[]
            → AutoWireLinkers()       ← FIX: tự động bind ID
        → EnterStage(startStage)
```

---

## 2. AutoWireLinkers — Tự động kết nối (không cần điền ID thủ công)

```
Với mỗi RequirementNodeData trong LevelData:
    req.SourceObjectID
        → tìm GameObject có ActionTargetID.ID == SourceObjectID trong scene
        → tìm component linker phù hợp với req.Type:
            Clicked      → ClickRequirementLinker
            DragComplete → DragRequirementLinker
            DropAccepted → DropRequirementLinker
            TimerExpired → (không có SourceObjectID, linker tự quản lý)
            Custom       → (designer tự set RequirementID thủ công)
        → linker.Bind(req.RequirementID)
        → nếu DropAccepted: dropLinker.BindAcceptedIDs(req.AcceptedDraggableIDs)
```

**Designer chỉ cần:**
1. Đặt component đúng loại (VD: `ClickRequirementLinker`) lên object trong scene
2. Object đó phải có `ActionTargetID` với ID khớp LevelData (editor tự tạo khi kéo thả)
3. **Không cần điền RequirementID bằng tay**

---

## 3. Luồng đầy đủ — Ví dụ Clicked

```
[User chạm màn hình]
    ↓
InputController.HandleFingerUp(finger)
    → kiểm tra di chuyển <= ClickMaxMoveDelta (10px)
    → RaycastFirst<Clickable>(screenPos)
        → Physics2D.OverlapPointNonAlloc tại vị trí world
        → trả về Clickable có sortingOrder cao nhất
    → clickable.OnTap()
    ↓
Clickable.OnTap()
    → ClickCount++
    → OnClicked.Invoke()
    ↓
ClickRequirementLinker.OnClicked()
    → _currentCount++
    → nếu _currentCount >= _requiredClickCount → Fulfill()
    ↓
RequirementLinker.Fulfill()
    → _isFulfilled = true
    → GamePlayController.Instance.FulfillRequirement(_requirementID)
    ↓
GamePlayController.FulfillRequirement(requirementID)
    → _fulfilled[requirementID] = true
    → ExecuteActionsForRequirement(requirementID, callback)
    → callback: CheckStageComplete()
```

---

## 4. ExecuteActionsForRequirement

```
FulfillRequirement(requirementID)
    → tìm RequirementNodeData có Data.RequirementID == requirementID
    → lấy tất cả ActionConnectionData có RequirementNodeID == reqNode.NodeID
    → với mỗi ActionNodeID → tìm ActionLinker trong scene
    → ActionLinker.Execute() theo thứ tự (Sequential nếu StageData.Sequential = true)
```

---

## 5. CheckStageComplete — Logic AND / OR Gate

```
EvaluateStageGates(currentStage)
    → lấy tất cả LogicGateConnectionData có StageID == currentStage.StageID
    → với mỗi gate:
        EvaluateGate(gate)
            → lấy tất cả RequirementConnectionData có LogicGateNodeID == gate.NodeID
            → AND Gate: tất cả requirement phải fulfilled → true
            → OR Gate: chỉ cần 1 requirement fulfilled → true
    → Stage pass nếu ÍT NHẤT 1 gate = true   (các gate là OR với nhau ở cấp stage)
    ↓
OnStageExited.Invoke(stageID)
    ↓
HandleTransition()
    → nếu StageID trong EndStageIDs → OnLevelComplete
    → FindMatchingTransition(stageID)
        → tìm TransitionData khớp FromStageID
        → kiểm tra RequiredFulfilledIDs (tất cả phải true)
    → WaitForSeconds(TimeDelayNext)
    → EnterStage(nextStage)
```

---

## 6. Sơ đồ nối các thành phần

```
RequirementNodeData
    ├─ Gate output ──→ LogicGateNodeData (AND/OR) ──→ StageData [Condition port]
    └─ Action output ──→ ActionNodeData ──→ ActionLinker (scene) ──→ thực thi animation/vị trí/...

RequirementNodeData.SourceObjectID
    └──→ ActionTargetID.ID (scene component)
            └──→ ClickRequirementLinker / DragRequirementLinker / DropRequirementLinker
                    └──→ GamePlayController.FulfillRequirement()
```

---

## 7. Những gì Designer cần làm trong scene

| Requirement Type | Component cần có trên object |
|---|---|
| Clicked | `Clickable` + `ClickRequirementLinker` |
| DragComplete | `Draggable` + `DragRequirementLinker` |
| DropAccepted | `DropZone` + `DropRequirementLinker` |
| TimerExpired | `TimerTrigger` + `TimerRequirementLinker` (set ID thủ công) |
| Custom | `RequirementLinker` subclass (set ID thủ công) |

> **Lưu ý:** Editor tự động add `Clickable`/`Draggable`/`DropZone` khi kéo object vào Requirement Node trong LevelEditor.
> Designer vẫn cần tự add `*RequirementLinker` tương ứng — hoặc có thể auto-add bằng `ComponentRequirementRegistry` sau này.

---

## 8. Các file liên quan

| File | Vai trò |
|---|---|
| `GamePlayController.cs` | Điều phối toàn bộ: load level, wire linkers, fulfill, transition |
| `RequirementLinker.cs` | Base class: Bind(), Fulfill(), RegisterListeners() |
| `ClickRequirementLinker.cs` | Lắng nghe Clickable.OnClicked |
| `DragRequirementLinker.cs` | Lắng nghe Draggable.OnDropped |
| `DropRequirementLinker.cs` | Lắng nghe DropZone.OnItemAccepted + filter AcceptedIDs |
| `TimerRequirementLinker.cs` | Lắng nghe TimerTrigger.OnExpired |
| `InputController.cs` | Nhận input LeanTouch → gọi Clickable.OnTap() / Draggable.OnBeginDrag() |
| `LevelData.cs` | ScriptableObject chứa toàn bộ graph data |
| `StageController.cs` | (Legacy) Xử lý stage completion độc lập |
