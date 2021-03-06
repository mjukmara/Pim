﻿using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System.Linq;

public class Pim : MonoBehaviour {

    public Animator animator;
    public Rigidbody2D rb;
    public Transform itemHolder;
    public int itemHolderSortingOrder = 30;
    public GameObject currentItem;
    public GameObject highlightEffect;
    public GameObject mergableEffect;
    private List<GameObject> highlightEffectCache = new List<GameObject>();
    public GameObject successfulMergeEffect;
    public GameObject blackMatterMergeEffect;

    public float movementSmoothing = 1.0f;

    public float speed = 2f;

    private int itemSortingOrderCache = -1;
    private Vector2 currentVelocity;

    // Event declaration
    public static event PimPickedUpItemEvent OnPimPickedUpItemEvent;
    public delegate void PimPickedUpItemEvent(Pim pim, Item item);
    public static event PimDroppedItemEvent OnPimDroppedItemEvent;
    public delegate void PimDroppedItemEvent(Pim pim, Item item);

    // Update is called once per frame
    void Update () {

        // Handle inputs
        Vector2 input = new Vector2(Input.GetAxisRaw("Horizontal"), Input.GetAxisRaw("Vertical")).normalized;
        Vector2 desiredVelocity = input * speed;

        Vector2 actualVelocity = Vector2.Lerp(currentVelocity, desiredVelocity, 1/movementSmoothing * Time.deltaTime);
        rb.velocity = actualVelocity;

        if (Input.GetKeyDown(KeyCode.Mouse0) || Input.GetKeyDown(KeyCode.Space)) {
            Interact();
        }
        currentVelocity = rb.velocity;

        animator.SetFloat("velocity", currentVelocity.magnitude);
        if (currentVelocity.x > 0) {
            animator.SetBool("facing_right", true);
            transform.localScale = new Vector3(1, 1, 1);
        }
        if (currentVelocity.x < 0) {
            animator.SetBool("facing_right", false);
            transform.localScale = new Vector3(-1, 1, 1);
        }
    }

    public void Interact() {
        List<GameObject> pickupPoints = GameObject.FindGameObjectsWithTag("PickupPoint").ToList<GameObject>();
        pickupPoints = pickupPoints.OrderBy(x => GetDistanceToObject(x)).ToList();

        foreach (GameObject pickupPointObj in pickupPoints) {
            PickupPoint pickupPoint = pickupPointObj.GetComponent<PickupPoint>();
            if (pickupPoint != null) {
                Collider2D pickupPointCollider = pickupPointObj.GetComponent<Collider2D>();
                if (pickupPointCollider != null) {
                    bool standingOnPickupPoint = pickupPointCollider.OverlapPoint(transform.position);

                    if (standingOnPickupPoint) {
                        if (pickupPoint.GetItem() != null) {
                            if (pickupPoint.CanPickupHere()) {
                                bool pickedUpItem = PickupItem(pickupPoint);

                                if (pickedUpItem) {
                                    AudioManager.instance.PlaySfx("Blip_Select5");
                                } else if (!pickedUpItem) {
                                    if (pickupPoint.MergeItem(currentItem)) {
                                        ClearAllHighlightedTiles(this, currentItem.GetComponent<Item>());
                                        Destroy(currentItem);
                                        currentItem = null;
                                        PickupItem(pickupPoint);


                                        if (currentItem) {
                                            Item mergedItem = currentItem.GetComponent<Item>();
                                            if (mergedItem) {
                                                if (mergedItem.itemType.name == "Black Matter") {
                                                    GameObject go = Instantiate(blackMatterMergeEffect, transform.position, Quaternion.identity);
                                                    go.transform.SetParent(transform);
                                                    AudioManager.instance.PlaySfx("BlackMattert");
                                                } else {
                                                    GameObject go = Instantiate(successfulMergeEffect, transform.position, Quaternion.identity);
                                                    go.transform.SetParent(transform);
                                                    AudioManager.instance.PlaySfx("Blip_Select10");
                                                }
                                            }
                                        }

                                    }
                                }
                            }
                        } else {
                            if (pickupPoint.CanDropHere()) {
                                if (DropItem(pickupPoint)) {
                                    AudioManager.instance.PlaySfx("Blip_Select7");
                                    EZCameraShake.CameraShaker.Instance.ShakeOnce(0.2f, 5.0f, 0.2f, 0.2f);
                                }
                            }
                        }
                    }
                }
            }
        }
    }

    void HighlightInteractableTiles(Pim pim, Item item) {

        List<GameObject> gos = GameObject.FindGameObjectsWithTag("PickupPoint").ToList<GameObject>();
        List<PickupPoint> pickupPoints = new List<PickupPoint>();
        foreach (var go in gos) {
            PickupPoint pickupPoint = go.GetComponent<PickupPoint>();
            if (pickupPoint) {
                pickupPoints.Add(pickupPoint);
            }
        }

        foreach (PickupPoint pickupPoint in pickupPoints) {
            //if (pickupPoint.CanDropHere()) {
                if (pickupPoint.IsOccupied()) {

                    GameObject recipesObj = GameObject.FindGameObjectWithTag("Recipes");
                    Recipes recipes = recipesObj.GetComponent<Recipes>();

                    GameObject itemObject = pickupPoint.GetItem();
                    if (itemObject != null) {
                        Item anItem = itemObject.GetComponent<Item>();
                        if (anItem != null) {
                            ItemType itemType = anItem.itemType;
                            Item currentItemObj = currentItem.GetComponent<Item>();
                            if (currentItem != null) {
                                Recipes.Recipe foundRecipe = recipes.GetRecipe(itemType, currentItemObj.itemType);

                                if (foundRecipe.output != null) {
                                    GameObject go = Instantiate(mergableEffect, pickupPoint.gameObject.transform.position, Quaternion.identity);
                                    highlightEffectCache.Add(go);
                                    go.transform.SetParent(itemObject.transform);
                                }
                            }
                        }
                    }
                } else {
                    if (pickupPoint.CanDropHere()) {
                        GameObject go = Instantiate(highlightEffect, pickupPoint.gameObject.transform.position, Quaternion.identity);
                        highlightEffectCache.Add(go);
                    }
                }
            //}
        }
    }

    void ClearAllHighlightedTiles(Pim pim, Item item) {
        foreach (GameObject go in highlightEffectCache) {
            Destroy(go);
        }
        highlightEffectCache.Clear();
    }

    bool PickupItem(PickupPoint pickupPoint) {
        if (IsHoldingItem()) return false;
        if (!pickupPoint.GetItem()) return false;

        currentItem = pickupPoint.GetItem();
        pickupPoint.RemoveItem();
        currentItem.transform.SetParent(itemHolder);
        currentItem.transform.SetPositionAndRotation(itemHolder.transform.position, Quaternion.identity);
        currentItem.transform.localScale = Vector3.one;

        SpriteRenderer spriteRenderer = currentItem.GetComponent<SpriteRenderer>();
        itemSortingOrderCache = spriteRenderer.sortingOrder;
        spriteRenderer.sortingOrder = itemHolderSortingOrder;

        OnPimPickedUpItemEvent(this, currentItem.GetComponent<Item>());

        return true;
    }

    bool DropItem(PickupPoint pickupPoint) {
        if (!IsHoldingItem()) return false;
        if (pickupPoint == null) return false;
        if (pickupPoint.GetItem()) return false;

        if (pickupPoint.AddItem(currentItem, false)) {
            currentItem.GetComponent<SpriteRenderer>().sortingOrder = itemSortingOrderCache;
            OnPimDroppedItemEvent(this, currentItem.GetComponent<Item>());
            currentItem = null;
        }

        return true;
    }

    bool IsHoldingItem() {
        return (currentItem != null);
    }

    float GetDistanceToObject(GameObject go) {
        return (go.transform.position - transform.position).magnitude;
    }

    void OnPickupPointRecievedItemEvent(PickupPoint pickupPoint, Item item) {

        List<GameObject> destroyTheseObjects = new List<GameObject>();

        bool isAlreadyCached = false;
        foreach (GameObject highlightedObject in highlightEffectCache) {
            if (highlightedObject == null) {
                destroyTheseObjects.Add(highlightedObject);
            } else {
                if (highlightedObject.transform.parent) {
                    if (highlightedObject.transform.parent.gameObject == item.gameObject) {
                        isAlreadyCached = true;
                        break;
                    }
                }
            }
        }

        if (!isAlreadyCached) {

            if (currentItem != null) {

                GameObject recipesObj = GameObject.FindGameObjectWithTag("Recipes");
                Recipes recipes = recipesObj.GetComponent<Recipes>();
                Item currentItemObj = currentItem.GetComponent<Item>();
                Recipes.Recipe foundRecipe = recipes.GetRecipe(item.itemType, currentItemObj.itemType);

                if (foundRecipe.output != null) {
                    GameObject go = Instantiate(mergableEffect, item.transform.position, Quaternion.identity);
                    highlightEffectCache.Add(go);
                    go.transform.SetParent(item.gameObject.transform);
                }
            }
        }

        foreach (GameObject go in destroyTheseObjects) {
            highlightEffectCache.Remove(go);
            Destroy(go);
        }
    }

    void OnEnable() {
        OnPimPickedUpItemEvent += HighlightInteractableTiles;
        OnPimDroppedItemEvent += ClearAllHighlightedTiles;
        PickupPoint.OnPickupPointRecievedItemEvent += OnPickupPointRecievedItemEvent;
    }

    void OnDisable() {
        OnPimPickedUpItemEvent -= HighlightInteractableTiles;
        OnPimDroppedItemEvent -= ClearAllHighlightedTiles;
        PickupPoint.OnPickupPointRecievedItemEvent -= OnPickupPointRecievedItemEvent;
    }
}
