# 從下載模型到可以玩的角色

這次使用 [Kenney Animated Characters Survivors](https://kenney.nl/assets/animated-characters-survivors)，在 Unity 6.6 的 Inspector 手動整理成 `ZombieCharacter.prefab`，再替換 Player 外觀。先停止 Play Mode。尺寸、朝向、材質和動畫都存在匯入設定或 Prefab，角色移動、碰撞與二段跳繼續由原本的 Player 控制。

## 1. 下載與挑選檔案

在素材頁按 Download，再選 Continue without donating，解壓縮。這次使用：

| ZIP 裡的位置 | 專案裡的位置 |
|---|---|
| `Model/characterMedium.fbx` | `Assets/NewCharacter/characterMedium.fbx` |
| `Animations/idle.fbx` | `Assets/NewCharacter/idle.fbx` |
| `Animations/run.fbx` | `Assets/NewCharacter/run.fbx` |
| `Animations/jump.fbx` | `Assets/NewCharacter/jump.fbx` |
| `Skins/zombieA.png` | `Assets/NewCharacter/zombieA.png` |
| `License.txt` | `Assets/NewCharacter/License.txt` |

在 Project 視窗的 Assets 底下右鍵 → Create → Folder，建立角色資料夾，再把上述檔案拖進去。這次成品放在 `Assets/NewCharacter`；跟著重做時請建立另一個資料夾，避免覆蓋現有成品。後面的路徑使用你的新資料夾即可。這份下載是 FBX 模型、動畫和貼圖，還不是已經接上遊戲的 Unity Prefab。

素材為 CC0，保留 `License.txt`。

## 2. 在匯入設定先處理尺寸、骨架與動畫

在 **Project 視窗選取 FBX 檔案**，右側才會顯示 Model／Rig／Animation／Materials 匯入分頁；不要選 Hierarchy 裡的模型實例。

1. 選 `characterMedium.fbx`，在 **Model** 設定下表前三項，按 **Apply**。
2. 切到 **Rig**，設定 Humanoid、Create From This Model，按 **Apply**。
3. 按 **Configure…**，確認必需骨骼映射為綠色、人物是 T-pose，再按 **Done** 回到匯入設定。
4. 分別選 `idle.fbx`、`run.fbx`、`jump.fbx`，重做相同的 Model／Rig 設定，每個分頁改完都按 **Apply**。

| 分頁 | 設定 | 這次使用的值／目的 |
|---|---|---|
| Model | Scale Factor | `0.28`：符合現有約一公尺高的玩家；其他模型要依實際大小調整 |
| Model | Convert Units | 開啟：使用 FBX 的單位換算 |
| Model | Bake Axis Conversion | 開啟：把座標軸轉換套到模型與動畫資料 |
| Rig | Animation Type | Humanoid |
| Rig | Avatar Definition | Create From This Model；確認 Configure 的骨骼映射有效 |
| Animation | Remove Constant Scale Curves | 開啟：移除與初始縮放相同的常數曲線；不是刪除所有縮放動畫 |

在三個動畫 FBX 的 **Animation** 分頁，先開啟 **Import Animation** 和 **Remove Constant Scale Curves**：

- 在 Clips 清單選 `Root|0.Targeting Pose`，按清單底下的 **−** 移除；每個檔案只留下 `Root|Idle`、`Root|Run` 或 `Root|Jump`。選取留下的 Clip 再修改下列選項。
- Idle、Run 開啟 Loop Time；Jump 關閉。
- Root Transform Rotation 開啟 Bake Into Pose，Based Upon 選 Original。
- Root Transform Position 的 Y、XZ 開啟 Bake Into Pose，Based Upon 選 Original。

本遊戲的升降與水平移動由 CharacterController 驅動，所以 Animator 不使用 Apply Root Motion。需要由動畫推動角色的遊戲，這些 Root Motion 設定要另外設計。

各檔案按 **Apply** 後，在 Inspector 底部展開 Preview、按播放，確認三個動畫有動作且尺寸一致；也在場景把模型與原 Player 並排，確認腳底與高度。綠色 Avatar 標記只代表骨骼映射有效，不能取代尺寸與動畫檢查。

如果已匯入的模型在修改 Scale Factor／單位後，播放動畫突然放大：在 Rig → Configure… 依序操作 **Mapping → Clear**、**Pose → Sample Bind-pose**、**Mapping → Automap**、**Pose → Enforce T-Pose**，按 Apply、Done，再預覽。若仍不正常，把下載的原始 FBX 匯入新資料夾，依上述順序先 Apply Model，再建立 Rig，確認成功後才替換引用。

Humanoid 是這包素材的做法；其他模型若無法建立 Humanoid Avatar，需要相容骨架的 Generic 動畫。

參考：[Kenney 作者的匯入步驟](https://kenney.nl/knowledge-base/game-assets-3d/importing-characters-and-animations)、[Unity 模型匯入設定](https://docs.unity3d.com/6000.6/Documentation/Manual/FBXImporter-Model.html)、[Unity Avatar 設定與重新映射](https://docs.unity3d.com/6000.6/Documentation/Manual/ConfiguringtheAvatar.html)。

## 3. 把模型組成可直接用的角色 Prefab

在 Hierarchy 的空白處右鍵 → **Create Empty**，命名 `ZombieCharacter`。在 Transform 元件選單按 **Reset**，讓 Position 0、Rotation 0、Scale 1。將 Project 裡的 `characterMedium.fbx` 拖到它底下：

```text
ZombieCharacter                 ← 遊戲效果作用在這層
└ characterMedium               ← 模型、骨架、Animator
```

子模型 Position 設為 0、Scale 設為 1。這個 Kenney 模型的腳尖原本朝 −Z，所以子模型 Rotation Y 設為 **180°**，讓準備好的角色朝 +Z。這是保存在 Prefab 的素材修正；不要在 Player.cs 開場再旋轉它。Bake Axis Conversion 處理座標系統，不能替你判斷人物臉朝哪邊。

在角色資料夾右鍵 → **Create → Material**，命名 `ZombieSkin`。Inspector 的 Shader 選 **Universal Render Pipeline/Lit**，把 `zombieA.png` 拖到 **Base Map**，旁邊的顏色設為白色。展開子模型，選有 **Skinned Mesh Renderer** 的物件，把材質拖到 **Materials → Element 0**；若有多個材質槽，逐一確認貼圖顯示正常。

在子模型的 Animator：

- 展開 Project 裡 `characterMedium.fbx` 左側的小箭頭，把其中的 Avatar 子資產拖到 **Avatar** 欄位。
- 在 Project 選 `Assets/Art/Animation/CharacterAnimatorController.controller`，按 **⌘D**（Windows：Ctrl+D）複製，將副本移到角色資料夾、改名 `ZombieAnimatorController`，拖到 **Controller** 欄位。
- 關閉 Apply Root Motion。

雙擊複製的 Controller，打開 Animator 視窗：

1. 展開 Project 裡三個動畫 FBX，找到 `Root|Idle`／`Root|Run`／`Root|Jump` Clip 子資產。
2. 雙擊 **Locomotion** 進入 Blend Tree，選 Blend Tree 節點，在 Inspector 把三個 Motion 依序換成 Idle、Run、Run。保留 **Speed** 參數、Threshold `0`／`0.5`／`0.7`，以及第三格原有的播放速度 `1.2`。
3. 回到 Base Layer，選 **Jump** 狀態，把 Motion 換成 Jump。
4. 選 Locomotion → Jump 的箭頭：Conditions 為 **Grounded = false**，關閉 **Has Exit Time**。
5. 選 Jump → Locomotion 的箭頭：Conditions 為 **Grounded = true**，關閉 **Has Exit Time**。保留其他轉場設定，以及 Parameters 裡的 **Speed（Float）**、**Grounded（Bool）**；Player.cs 會更新它們。

這樣起跳與落地會響應 Grounded，不必等待走路／待機動畫播到指定位置。

把整個 `ZombieCharacter` 拖到 Project 視窗，保存為 `Assets/NewCharacter/ZombieCharacter.prefab`。它的外層 Transform 是 0／0／1，材質、Animator 與朝向已接好。

## 4. 替換 Player 的外觀

先停止 Play Mode，打開 `Assets/Prefabs/Player.prefab`：

1. 把 `ZombieCharacter.prefab` 拖到 Player 底下，命名為 `character`，Position 0、Rotation 0、Scale 1。
2. Player 元件的 **Model** 指定新的 `character` 外層。
3. **Animator** 指定新角色子模型上的 Animator。
4. 確認引用已改好，再移除舊角色子物件，儲存 Player Prefab。

```text
Player                          ← Player.cs、CharacterController
├ character                     ← 已準備好的 ZombieCharacter Prefab
│ └ characterMedium              ← Animator、骨架、模型
├ DustParticles
└ 其他原有音效／效果物件
```

保留 Player 的移動、Input Actions、碰撞膠囊與效果引用。新角色與舊角色大小接近時，可以沿用膠囊；換成不同身形時，要在 Scene 視窗確認高度、半徑和腳底接地。

Player.cs 只記住 Model 初始大小，跳躍／落地相對這個大小拉伸、再恢復。它不再修正模型朝向。獨立外層也讓骨架動畫與跳躍變形作用在不同物件上。

## 5. 現場驗證

![整理好的 ZombieCharacter 在原本關卡實際執行](Images/zombie-game.png)

退出 Prefab Mode，打開 `Assets/Scenes/Main.unity`，按 Play：

- 不移動時播放 Idle，WASD 移動時播放 Run，臉和腳尖朝移動方向。
- Space 起跳、再按一次二段跳；空中播放 Jump，落地回到 Idle／Run。
- 腳底接地，沒有突然放大，跳躍／落地的變形之後恢復原尺寸。
- 收一枚 Coin，HUD 加分；Console 沒有新增錯誤。

停止 Play Mode 後保留準備好的角色 Prefab；以後替換相同角色可以直接使用它。

## 已有 Unity Prefab 時

1. 先確認來源要求的 Unity 版本、Render Pipeline 與依賴。本專案使用 URP。
2. `.unitypackage` 用 **Assets → Import Package → Custom Package…** 匯入，保留角色依賴的模型、貼圖、材質、動畫和 Controller；若來源是整個專案資料夾，也要一起帶入必要資產及它們的 `.meta`，保留引用。單獨一個 `.prefab` 檔通常不包含這些內容。
3. 拖到場景檢查尺寸、朝向、腳底、材質和動畫。在 Hierarchy 右鍵這個實例 → **Prefab → Unpack Completely**，做成自己可編輯的角色；保留下載的原始 Prefab。需要改 FBX 尺寸／骨架則回到第 2 步。
4. 在這個已解開的實例上，從 Inspector 移除來源的移動腳本，以及與本專案 Player 重複的 Collider／Rigidbody。按第 3 節整理外層與 Animator，保存為自己的角色 Prefab。
5. 本專案會更新 `Speed`／`Grounded`；若來源 Controller 使用不同參數，按第 3 步接到角色專用 Controller，再按第 4、5 步替換與實玩。Humanoid 動畫需要有效 Avatar；Generic 動畫必須匹配骨架，不能只換 Mesh 就保證沿用全部動畫。

參考：[Unity 解開 Prefab 實例](https://docs.unity3d.com/6000.6/Documentation/Manual/UnpackingPrefabInstances.html)。
