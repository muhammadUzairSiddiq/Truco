# Exact proposed wallet/admin contract implemented by Unity

These paths and payloads are newly defined integration requirements. They are not a claim that the backend already implements them. All coin values are integer Trucoins; balances, limits, ownership, permissions and mutations remain server authority.

## Routes / SO keys

| Key | Method | Path |
|---|---|---|
| WalletSnapshot | GET | /wallet/snapshot |
| WalletCreateRequest | POST | /wallet/requests |
| WalletRequestStatus | GET | /wallet/requests/{id} |
| AdminWalletRequestList | GET | /admin/wallet/requests |
| AdminWalletRequestDetail | GET | /admin/wallet/requests/{id} |
| AdminWalletRequestApprove | POST | /admin/wallet/requests/{id}/approve |
| AdminWalletRequestReject | POST | /admin/wallet/requests/{id}/reject |
| AdminWalletRequestPay | POST | /admin/wallet/requests/{id}/pay |

The SO template uses `{0}` where this table uses `{id}`. Admin list supports `limit=20` (1..100), optional opaque `cursor`, optional `type=buy|sell`, and returns `{success:true,data:{items:[record],nextCursor:null|string}}`. Admin detail optionally receives `operationKey=<idempotencyKey>` and supplies a matching `operationReceipt` if that operation was applied. Unknown or in-flight keys return operationReceipt=null without claiming success.

## Player schemas

Snapshot success: `{success:true,data:{available:297,held:50,configuration:{minimumBuy:5,minimumSell:50,maximumAmount:5000,buyPresets:[50,60,75,100,150,200],sellPresets:[50,100,150,200],identificationRequired:true},requests:[request]}}`.

Create body: `{type:"buy"|"sell",amount:50,bank?:string,alias?:string,holder?:string,identification?:string,whatsapp?:string,idempotencyKey:string}`. Sale requires bank, alias/account, holder, international WhatsApp and identification when configuration requires it. Buy ignores payout fields. Identity MUST derive from Bearer authentication, never a submitted user ID. Validate amount against current limits and available balance atomically.

Create/status success has `data=request` directly. Request fields: `{id,type,amount,status,createdAt,bank,alias,holder,whatsapp,receiptUrl,history:[{status,at}]}`. `createdAt` and history.at are UTC ISO8601. Bank/contact instructions for a purchase come from the server. receiptUrl is an owned HTTPS upload page and opening it is NOT confirmation that the receipt was uploaded. The upload service updates receipt_sent only after it durably stores the real receipt. Status values: requested, receipt_sent, verification, approved, credited, paid, finalized, rejected, cancelled. created/pending are accepted legacy labels. Snapshot history must return all requests for this contract version; a future paginated player history requires a matching client extension.

## Admin record / mutation schemas

Every admin route must require an authenticated server role=admin and enforce authorization independently of UI. Unity additionally calls /auth/check-admin before EVERY admin list/detail/mutation; return `{ok:true,role:"admin"}` only for authorized admins.

Detail `data` and each list item are a record:

`{request:<player request>,owner:{id,username},revision:3,allowedActions:["approve","reject","pay"],receiptDownloadUrl:null|https-url,audit:[{id,action,actorId,at,reason,idempotencyKey,revision}],operationReceipt:null|operation}`.

revision is a non-negative integer incremented on every state change. owner.id, request.id/type/amount/status must be present. allowedActions is server-computed for this exact current record/actor; empty or missing means Unity enables no mutation. receiptDownloadUrl is an authorized short-lived HTTPS view/download URL, never an upload-success claim. Audit events come from durable server storage, newest first, and are not fabricated from local clicks.

All three mutation bodies:

`{expectedRevision:3,reason:"Comprobante revisado",idempotencyKey:"stable-unique-key",paymentReference:"BANK-123"}`.

reason is 3..500 characters. paymentReference is required for pay and is 3..200 characters; ignored otherwise. No amount, user ID, bank details or resulting status may be supplied by the mutation body. Use stored request ownership and amount.

Mutation success:

`{success:true,data:{record:<updated record>,operation:{id:"op-id",requestId:"request-id",action:"approve"|"reject"|"pay",idempotencyKey:"same-key",revision:4,applied:true}}}`.

Unity verifies matching request, owner, type, amount, action/key, applied=true, operation ID and increasing revision. A generic HTTP200 or success=true alone cannot claim approval/payment.

- Approve BUY: after actual transfer/receipt verification, credit available balance exactly once and return status=credited. Unity will reject a bare approved response as incomplete for this action.
- Approve SELL: keep the existing hold and return approved; no second debit/hold.
- Reject an unpaid request: return rejected. For a sale, atomically release its original hold exactly once. A rejected buy receives no credit.
- Pay SELL: require an approved sale, real external-transfer reference and verified admin authority. Convert the existing hold to a final debit exactly once; return paid or finalized. The Unity button records a completed transfer; Unity does not perform a bank transfer.
- Paid/finalized/credited operations must not be reversed by replaying these endpoints. Financial reversals require separately authorized server processes.

## Idempotency / concurrency / errors

Persist the key, authenticated actor, operation/request identity, canonical payload hash and full result durably. Repeating an identical key/body returns the SAME operation ID/result even if expectedRevision has since advanced; check idempotency BEFORE revision validation. Reusing a key for a different payload returns409. A new key with stale expectedRevision returns409 without mutation. The ledger entry, balance movement, request transition, revision and audit event commit atomically. An applied receipt must not be returned before commit.

After timeout the Unity screen retains the exact command/key for retry. Detail with operationKey supports reconciliation. allowedActions and revisions must also prevent a restarted client/new key from applying a completed transition twice. No timeout-triggered automatic POST retry occurs. Existing401 authentication refresh retries the same immutable body/key after successful refresh, so authorize before mutation.

All endpoints require Bearer auth; mutations also receive x-nonce and x-timestamp (Unix milliseconds). Maintain replay protection separately from durable idempotency. Reject expired/reused nonces without performing side effects.

Errors use non-2xx and `{success:false,code:"REVISION_CONFLICT",error:"La solicitud cambió. Actualizá antes de continuar.",message:"La solicitud cambió. Actualizá antes de continuar.",requestId:"correlation-id"}`. error/message are safe human text; no secrets, stack traces, cookies or raw payment credentials. Statuses:400 validation,401 unauthenticated,403 unauthorized,404 absent/not-owned player request,409 revision/idempotency conflict,422 invalid transition,429 rate limit,503 unavailable/maintenance. Standard Retry-After for429. Return no fake successful empty records on backend errors.

Snapshot and user-status reads enforce ownership. Admin reads enforce roles and audit access. blockGame/blockTransactions maintenance must prevent new player wallet mutations server-side too; do not let a stale client flag override backend policy. Admin recovery permissions during maintenance must be explicit and audited.


## General administrative audit (AdminAudit)

GET `/admin/audit?limit=5&cursor=<opaque>` requires a freshly verified backend admin role. limit is1..100; cursors are opaque. Response: `{success:true,data:{items:[{id,at,actorId,action,targetType,targetId,reason,detail}],nextCursor:null|string}}`. id/at/actorId/action are required nonempty strings; at is UTC ISO8601. The server writes immutable events as part of actual authorized mutations, derives actor identity from authentication and returns newest first. Never accept client-supplied audit events as proof that a financial/admin operation happened. Return non-2xx for errors, never a fake empty success.

Unity's control-center audit now loads this server feed with loading/error/empty states and previous/next-page controls. The controls are scrollable so all events and actions remain reachable. Local `TrucoAdminAuditLog` remains diagnostic storage only and is no longer displayed as the authoritative feed.

Maintenance coordination uses the parent contract's GET `/system/status` and POST `/admin/system/maintenance`, returning `{success:true,data:{maintenance:<canonical state>}}`. Only confirmed canonical state is applied and displayed; a bare HTTP200/success without that state is insufficient. These two routes are owned by the parent OpenAPI document and intentionally are not duplicated in this wallet fragment.

## Exceptional administrative mutations

The existing two admin UIs now use `TrucoAdminOperationService`. Player tournament finalization is separate: setting an arbitrary champion uses `AdminSetChampion`, POST `/admin/tournaments/{id}/champion`, never the player `/tournaments/{id}/finalize` route. Body: `{action:"set_champion",tournamentId,championId,reason:"admin-set-champion",idempotencyKey}`. The server must verify actual admin role, tournament existence, eligible champion membership and transition policy. This command confirms champion assignment only; it does not claim prize payment.

`AdminForceCloseTournament`, POST `/admin/tournaments/{id}/force-close`, receives `{action:"force_close_tournament",tournamentId,reason,idempotencyKey}`. Confirmed receipt additionally requires `status:"closed",refundsApplied:true,refundedTotal:<integer0..2147483647>`. The server must close/cancel the tournament, prevent further play and settle every eligible entry refund exactly once atomically with the durable operation/audit. Zero refundedTotal is valid only when no refundable entry exists; never return refundsApplied before all required refunds commit.

`AddUserCoins`, POST `/admin/users/{id}/coins`, receives `{action:"credit_coins",userId,amount:<integer1..2147483647>,reason,idempotencyKey}`. Receipt additionally requires the exact userId/amount, integer nonnegative balanceAfter and nonempty ledgerTransactionId. Server permission, positive amount, reason, limits, balance overflow and exceptional-credit policy checks must precede the atomic ledger/balance/audit commit.

All three replies use `{success:true,data:{operation:{id,action,idempotencyKey,applied:true,actorId,at,reason,...action-specific fields}}}`. actorId is derived from auth, at is UTC ISO8601, path id must equal command target. The receipt must match the requested action, key, reason and target/amount/champion exactly. Bare HTTP200, success:true or an accepted/queued operation never confirms completion.

Every actual POST is preceded by a fresh server admin-role check. Identical concurrent intents coalesce. Timeout/unconfirmed outcomes retain the exact command body/key; there is no timeout-triggered automatic POST retry. A random key is also retained across restart under a SHA256 actor+canonical-intent PlayerPrefs name; no raw reason/user/payment body is stored there. After a confirmed receipt the key is cleared; a subsequent explicitly confirmed UI action is a new intent. Persist deduplication records durably server-side, scoped to authenticated actor+key+canonical payload, and check existing identical keys before current-state validation. Replays return the same operation/ledger/audit IDs, never a second credit/refund. Authentication refresh may retry the same immutable body/key. These adapters use the JSON idempotencyKey; no custom Idempotency-Key HTTP header is required.

The UI now reports unconfirmed outcomes explicitly and instructs reviewing the authoritative audit or repeating the same action/data. Backend implementation, live financial acceptance and full release verification remain required.
