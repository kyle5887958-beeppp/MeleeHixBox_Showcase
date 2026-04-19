using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
/*核心功能：處理近戰武器的物理判定與受擊邏輯。
 備註：
 1. 使用 CapsuleCast 搭配上一幀座標快照，解決高速揮劍時的穿透問題。
 2. 整合 IDamageable 介面與 HitStop 停頓感，目前彈刀邏輯先寫死在 Shield Layer 判定。*/

public class MeleeHitBox : MonoBehaviour
{
    [Header("檢測點")]
    public Transform tipPoint;
    public Transform basePoint;

    [Header("判定設定")]
    public float weaponRadius = 0.05f;
    public LayerMask WeaponHitLayer;
    public GameObject sparkPrefab;

    [Header("傷害設定")]
    public float currentPower = 15f;
    public float currentPoise = 10f;

    private Vector3 lastTipPos;
    private Vector3 lastBasePos;
    private List<Transform> hitEnemies = new List<Transform>();

    [Header("Debug")]
    public bool showGizmos = true;
    public Color gizmoColor = Color.cyan;
    [SerializeField] private bool isAttacking = false;

    Animator animator;
    AudioSource audioSource;

    [Header("Settings")]
    public AudioClip hitShieldSound;
    [Range(0, 1)] public float volume = 0.8f;

    void Awake()
    {
        animator = GetComponentInParent<Animator>();
        audioSource = GetComponentInParent<AudioSource>();
        audioSource.playOnAwake = false;
        audioSource.spatialBlend = 1.0f;
    }

    public void DamageOn()
    {
        isAttacking = true;
        hitEnemies.Clear();
        // 紀錄起始位置，避免第一幀因為上一段攻擊的殘留座標拉出一條超長射線
        lastTipPos = tipPoint.position;
        lastBasePos = basePoint.position;
    }

    public void DamageClose()
    {
        isAttacking = false;
    }

    void FixedUpdate()
    {
        if (!isAttacking) return;

        // 計算這幀與上一幀之間的位移量
        Vector3 currentMid = (tipPoint.position + basePoint.position) * 0.5f;
        Vector3 lastMid = (lastTipPos + lastBasePos) * 0.5f;

        Vector3 castDirection = currentMid - lastMid;
        float castDistance = castDirection.magnitude;

        if (castDistance > 0)
        {
            // 使用 CapsuleCast 補足兩幀之間的空隙，防止高速揮劍時穿透敵人
            RaycastHit[] hits = Physics.CapsuleCastAll(
                lastBasePos,
                lastTipPos,
                weaponRadius,
                castDirection.normalized,
                castDistance,
                WeaponHitLayer
            );

            foreach (var hit in hits)
            {
                // 統一取得 Root，避免同一幀內重複擊中同一個敵人的不同部位
                Transform enemyRoot = hit.collider.transform.root;

                if (!hitEnemies.Contains(enemyRoot))
                {
                    hitEnemies.Add(enemyRoot);
                    OnHit(hit);
                }
            }
        }

        lastTipPos = tipPoint.position;
        lastBasePos = basePoint.position;
    }

    void OnHit(RaycastHit hit)
    {
        // HitStop 處理
        HitStopManager hitStop = GetComponentInParent<HitStopManager>();
        if (hitStop != null)
        {
            Animator targetAnimator = hit.collider.GetComponentInParent<Animator>();
            hitStop.TriggerStop(0.08f, targetAnimator);
        }

        IDamageable target = hit.collider.GetComponentInParent<IDamageable>();
        if (target == null) return;

        // 彈刀邏輯：檢查是否撞到盾牌 Layer
        int shieldLayer = LayerMask.NameToLayer("Shield");
        DamageData data = new DamageData(currentPower, currentPoise, gameObject, hit.point);

        if (hit.collider.gameObject.layer == shieldLayer)
        {
            if (sparkPrefab != null)
            {
                // 計算特效生成點：取劍身軸線上最接近碰撞點的位置
                Vector3 swordMid = (tipPoint.position + basePoint.position) * 0.5f;
                Vector3 closestOnCollider = hit.collider.ClosestPoint(swordMid);
                Vector3 spawnPos = ClosestPointOnSegment(basePoint.position, tipPoint.position, closestOnCollider);

                Quaternion spawnRot = hit.normal != Vector3.zero ? Quaternion.LookRotation(hit.normal) : Quaternion.identity;
                GameObject spark = Instantiate(sparkPrefab, spawnPos, spawnRot);
                Destroy(spark, 1.0f);
            }

            TriggerRecoil();
            StartCoroutine(DisableMovementForRecoil(1.15f));
            target.TakeDamage(data);
            return;
        }

        target.TakeDamage(data);
    }

    private Vector3 ClosestPointOnSegment(Vector3 a, Vector3 b, Vector3 point)
    {
        Vector3 ab = b - a;
        float t = Mathf.Clamp01(Vector3.Dot(point - a, ab) / Vector3.Dot(ab, ab));
        return a + t * ab;
    }

    private void TriggerRecoil()
    {
        if (animator != null)
        {
            // 播放彈刀受擊動畫
            animator.CrossFade("OneHand_Base_Shield_Block_Hit_4_Test", 0.05f);
            audioSource.PlayOneShot(hitShieldSound, volume);
        }
    }

    public IEnumerator DisableMovementForRecoil(float duration)
    {
        // TODO: 這裡要切換角色狀態機，暫時只留時間佔位
        yield return new WaitForSeconds(duration);
    }

    void OnDrawGizmos()
    {
        if (!showGizmos || basePoint == null || tipPoint == null) return;

        Gizmos.color = gizmoColor;
        Gizmos.DrawLine(basePoint.position, tipPoint.position);
        Gizmos.DrawWireSphere(basePoint.position, weaponRadius);
        Gizmos.DrawWireSphere(tipPoint.position, weaponRadius);

        if (isAttacking)
        {
            Gizmos.color = Color.red;
            Gizmos.DrawLine(lastBasePos, basePoint.position);
            Gizmos.DrawLine(lastTipPos, tipPoint.position);
        }
    }
}