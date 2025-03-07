using Dalamud.Interface.Windowing;
using FFXIVClientStructs.FFXIV.Client.Game;
using FFXIVClientStructs.FFXIV.Client.UI;
using FFXIVClientStructs.FFXIV.Client.UI.Agent;
using FFXIVClientStructs.FFXIV.Component.GUI;
using FFXIVClientStructs.Interop;
using ImGuiNET;
using Lumina.Excel.Sheets;
using System;
using System.Runtime.InteropServices;

namespace CopeSeetheMeld.Windows;

internal unsafe class MeldWindow : Window
{
    private readonly InventoryManager* inventoryManager = InventoryManager.Instance();
    private readonly AgentMateriaAttach* agentAttach = AgentMateriaAttach.Instance();
    private readonly AgentMaterialize* agentMaterialize = AgentMaterialize.Instance();
    private InventoryItem* selectedItem;
    private InventoryItem* selectedMateria;
    public MeldWindow() : base("Meld") { }

    public override void Draw()
    {
        if (selectedItem != null)
            DrawFocusedItem(selectedItem, selectedItem->ItemId.ItemRow());
        else
        {
            IterAll(pt =>
            {
                var item = pt.Value;
                var row = item->ItemId.ItemRow();
                if (CanMeld(item))
                {
                    row.Draw();
                    if (ImGui.IsItemHovered())
                        ImGui.SetTooltip(row.Name.ToString());
                    if (ImGui.IsItemClicked())
                        SelectItem(item);
                    ImGui.SameLine();
                }
            });
        }

        ImGui.NewLine();
        if (ImGui.Button("Reset"))
        {
            selectedMateria = null;
            selectedItem = null;
            agentAttach->SelectedItem = -1;
            if (agentAttach->AddonIdAttachDialog > 0)
                RaptureAtkModule.Instance()->CloseAddon(agentAttach->AddonIdAttachDialog);
            TriggerEvent(agentAttach, 6, [1, 0, 0]);
        }

        Dalamud.Utility.Util.ShowStruct(agentAttach);
    }

    private void DrawFocusedItem(InventoryItem* item, Item row)
    {
        row.Draw();
        if (ImGui.IsItemClicked())
        {
            selectedItem = null;
            selectedMateria = null;
            return;
        }
        ImGui.SameLine();
        ImGui.TextUnformatted($"{row.Name}");

        if (selectedMateria != null)
            DrawFocusedMateria(selectedMateria, selectedMateria->ItemId.ItemRow());
        else if (!HasSlots(item))
        {
            if (ImGui.Button("Retrieve"))
                TriggerEvent(agentAttach, 4, [0, 1, 0, 0, 0]);
        }
        else
            IterAll(pt =>
            {
                var item = pt.Value;
                var row = item->ItemId.ItemRow();
                if (row.FilterGroup != 13)
                    return;

                row.Draw();
                if (ImGui.IsItemHovered())
                    ImGui.SetTooltip(row.Name.ToString());
                if (ImGui.IsItemClicked())
                    SelectMateria(item);
                ImGui.SameLine();
            });
    }

    private void DrawFocusedMateria(InventoryItem* materia, Item row)
    {
        row.Draw();
        if (ImGui.IsItemClicked())
        {
            selectedMateria = null;
            return;
        }
        ImGui.SameLine();
        ImGui.TextUnformatted($"{row.Name}");
    }

    private unsafe void IterAll(Action<Pointer<InventoryItem>> action)
    {
        IterContainer(InventoryType.Inventory1, action);
        IterContainer(InventoryType.Inventory2, action);
        IterContainer(InventoryType.Inventory3, action);
        IterContainer(InventoryType.Inventory4, action);
    }

    private void IterContainer(InventoryType ty, Action<Pointer<InventoryItem>> action)
    {
        var cont = inventoryManager->GetInventoryContainer(ty);
        if (!cont->IsLoaded)
        {
            Plugin.Log.Warning($"unable to load inventory {ty}, it will be skipped");
            return;
        }

        for (var i = 0; i < cont->Size; i++)
        {
            var slot = cont->GetInventorySlot(i);
            if (slot->ItemId != 0)
                action(slot);
        }
    }

    private void SelectItem(InventoryItem* item)
    {
        selectedItem = item;

        for (var i = 0; i < agentAttach->ItemCount; i++)
            if (item == *agentAttach->Context->Items[i])
            {
                TriggerEvent(agentAttach, 0, [1, i, HasSlots(item) ? 1 : 0, 0]);
                break;
            }
    }

    private void SelectMateria(InventoryItem* materia)
    {
        selectedMateria = materia;

        for (var i = 0; i < agentAttach->MateriaCount; i++)
            if (materia == *agentAttach->Context->Materia[i])
            {
                TriggerEvent(agentAttach, 0, [2, i, 1, 0]);
                break;
            }
    }

    private static bool HasSlots(InventoryItem* item) => item->GetMateriaCount() < item->ItemId.ItemRow().MateriaSlotCount;
    private static bool CanMeld(InventoryItem* item) => item->ItemId.ItemRow().MateriaSlotCount > 0;

    private AtkValue TriggerEvent(AgentMateriaAttach* agent, ulong eventKind, int[] args) => TriggerEvent(&agent->AgentInterface, eventKind, args);

    private AtkValue TriggerEvent(AgentInterface* agent, ulong eventKind, int[] args)
    {
        var atkret = new AtkValue();
        var ma = MakeArgs(args);
        try
        {
            agent->ReceiveEvent(&atkret, ma, (uint)args.Length, eventKind);
        }
        finally
        {
            Marshal.FreeHGlobal(new nint(ma));
        }
        return atkret;
    }

    private AtkValue* MakeArgs(params int[] args)
    {
        var values = (AtkValue*)Marshal.AllocHGlobal(args.Length * sizeof(AtkValue));
        for (var i = 0; i < args.Length; i++)
        {
            values[i].Type = FFXIVClientStructs.FFXIV.Component.GUI.ValueType.Int;
            values[i].Int = args[i];
        }
        return values;
    }
}
