﻿using Barotrauma.Items.Components;
using Barotrauma.Networking;
using System;
using System.Collections.Generic;
using System.Text;
using System.Xml.Linq;

namespace Barotrauma
{
    class CharacterCampaignData
    {
        public readonly CharacterInfo CharacterInfo;

        public readonly string Name;

        public readonly string ClientIP;
        public readonly ulong SteamID;

        private XElement itemData;

        public CharacterCampaignData(Client client)
        {
            Name = client.Name;
            ClientIP = client.Connection.RemoteEndPoint.Address.ToString();
            SteamID = client.SteamID;
            CharacterInfo = client.CharacterInfo;

            if (client.Character.Inventory != null)
            {
                itemData = new XElement("inventory");
                SaveInventory(client.Character.Inventory, itemData);
            }
        }

        private void SaveInventory(Inventory inventory, XElement parentElement)
        {
            for (int i = 0; i < inventory.Items.Length; i++)
            {
                if (inventory.Items[i] == null) continue;
                inventory.Items[i].Submarine = inventory.Owner.Submarine;
                var itemElement = inventory.Items[i].Save(parentElement);
                itemElement.Add(new XAttribute("i", i));

                foreach (ItemContainer container in inventory.Items[i].GetComponents<ItemContainer>())
                {
                    XElement childInvElement = new XElement("inventory");
                    itemElement.Add(childInvElement);
                    SaveInventory(container.Inventory, childInvElement);
                }
            }
        }

        public CharacterCampaignData(XElement element)
        {
            Name = element.GetAttributeString("name", "Unnamed");
            ClientIP = element.GetAttributeString("ip", "");
            string steamID = element.GetAttributeString("steamid", "");
            if (!string.IsNullOrEmpty(steamID))
            {
                ulong.TryParse(steamID, out SteamID);
            }

            foreach (XElement subElement in element.Elements())
            {
                if (subElement.Name.ToString().ToLowerInvariant() == "characterinfo")
                {
                    CharacterInfo = new CharacterInfo(subElement);
                    CharacterInfo.PickedItemIDs.Clear();
                    break;
                }
            }
        }

        public bool MatchesClient(Client client)
        {
            if (SteamID > 0)
            {
                return SteamID == client.SteamID;
            }
            else
            {
                return ClientIP == client.Connection.RemoteEndPoint.Address.ToString();
            }
        }

        public XElement Save()
        {
            XElement element = new XElement("CharacterCampaignData", 
                new XAttribute("name", Name),
                new XAttribute("ip", ClientIP),
                new XAttribute("steamid", SteamID));

            CharacterInfo?.Save(element);

            return element;
        }

        public void SpawnInventoryItems(Inventory inventory)
        {
            foreach (XElement itemElement in itemData.Elements())
            {
                var newItem = Item.Load(itemElement, inventory.Owner.Submarine);
                int slotIndex = itemElement.GetAttributeInt("i", 0);
                if (newItem == null) continue;

                Entity.Spawner.CreateNetworkEvent(newItem, false);
                inventory.TryPutItem(newItem, slotIndex, false, false, null);

                int itemContainerIndex = 0;
                var itemContainers = newItem.GetComponents<ItemContainer>();
                foreach (XElement childInvElement in itemElement.Elements())
                {
                    if (childInvElement.ToString().ToLowerInvariant() != "inventory") continue;
                    if (itemContainerIndex >= itemContainers.Count) break;
                    SpawnInventoryItems(itemContainers[itemContainerIndex].Inventory);
                    itemContainerIndex++;
                }
            }
        }
    }
}
