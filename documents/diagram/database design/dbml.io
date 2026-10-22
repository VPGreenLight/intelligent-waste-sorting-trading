// ==========================================
// ENUMS (Kiểu dữ liệu liệt kê)
// ==========================================

Enum user_role {
  User
  Recycler
  Staff
  Admin
}

Enum user_status {
  Pending_Verification
  Active
  Locked
  Deactivated
}

Enum verification_status {
  Pending
  Verified
  Rejected
}

Enum active_status {
  Online
  Offline
}

Enum category_status {
  Active
  Inactive
}

Enum report_status {
  Pending
  Reviewed
  Rejected
}

Enum request_status {
  Pending
  Negotiating
  Accepted
  Scheduled
  Completed
  Rejected
  Cancelled
  Disputed
  Expired
}

Enum complaint_status {
  Pending
  Investigating
  Resolved
  Dismissed
}

Enum order_status {
  Pending
  Completed
  Expired
  Mismatched
}

Enum action_type {
  Handover_Reward
  Daily_Checkin
  Redeem_Gift
  Tree_Care
}

Enum gift_type {
  Physical
  Voucher
}

Enum redemption_status {
  Redeemed
  Delivered
  Expired
}

Enum post_status {
  Draft
  Pending_Approval
  Approved
  Rejected
}

// ==========================================
// 1. PHÂN HỆ AUTH & TÀI KHOẢN
// ==========================================

Table User {
  user_id integer [pk, increment, note: "ID tài khoản"]
  email varchar [not null, unique]
  password_hash varchar [not null]
  full_name varchar
  phone_number varchar
  avatar_url varchar
  role user_role [not null]
  status user_status [not null, default: 'Active']
  is_google_auth boolean [default: false]
  created_at timestamp [default: `now()`]
  updated_at timestamp
}

Table RecyclerProfile {
  recycler_id integer [pk, ref: - User.user_id, note: "Khoá chính 1-1 với User"]
  facility_name varchar [not null]
  representative_name varchar
  address text [not null]
  latitude decimal(10,8)
  longitude decimal(11,8)
  operating_hours varchar
  legal_permit_url varchar
  business_photo_url varchar
  verification_status verification_status [default: 'Pending']
  active_status active_status [default: 'Offline']
  rejection_reason text
}

Table StaffProfile {
  staff_id integer [pk, ref: - User.user_id, note: "Khoá chính 1-1 với User"]
  staff_code varchar [not null, unique]
  department varchar
  position varchar
}

Table PasswordResetToken {
  token_id integer [pk, increment]
  user_id integer [ref: > User.user_id]
  token_code varchar [not null]
  expired_at timestamp [not null]
  is_used boolean [default: false]
}

// ==========================================
// 2. PHÂN HỆ AI & DANH MỤC RÁC
// ==========================================

Table MasterWasteCategory {
  category_id integer [pk, increment]
  category_code varchar [not null, unique]
  category_name varchar [not null]
  material_group varchar
  description text
  icon_url varchar
  status category_status [default: 'Active']
}

Table SortingGuidanceRule {
  rule_id integer [pk, increment]
  category_id integer [ref: > MasterWasteCategory.category_id]
  handling_steps text
  destination_bin varchar
  updated_at timestamp
}

Table AIScanHistory {
  scan_id integer [pk, increment]
  user_id integer [ref: > User.user_id]
  image_url varchar [not null]
  predicted_category_id integer [ref: > MasterWasteCategory.category_id]
  confidence_score decimal(5,2)
  scanned_at timestamp [default: `now()`]
}

Table MisclassifiedReport {
  report_id integer [pk, increment]
  scan_id integer [unique, ref: - AIScanHistory.scan_id]
  user_id integer [ref: > User.user_id]
  image_url varchar
  predicted_category_id integer [ref: > MasterWasteCategory.category_id]
  user_suggested_category_id integer [ref: > MasterWasteCategory.category_id]
  staff_verified_category_id integer [ref: > MasterWasteCategory.category_id]
  note text
  status report_status [default: 'Pending']
  reviewed_by_staff_id integer [ref: > StaffProfile.staff_id]
}

Table VerifiedDataset {
  dataset_id integer [pk, increment]
  image_url varchar [not null]
  verified_category_id integer [ref: > MasterWasteCategory.category_id]
  labeled_by_staff_id integer [ref: > StaffProfile.staff_id]
  created_at timestamp [default: `now()`]
}

// ==========================================
// 3. PHÂN HỆ THU GOM, THƯƠNG LƯỢNG & BÀN GIAO
// ==========================================

Table RecyclerPriceList {
  price_item_id integer [pk, increment]
  recycler_id integer [ref: > RecyclerProfile.recycler_id]
  category_id integer [ref: > MasterWasteCategory.category_id]
  reference_price_per_kg decimal(12,2)
  min_weight_kg decimal(8,2)
  is_active boolean [default: true]
}

Table HandoverRequest {
  request_id integer [pk, increment]
  user_id integer [ref: > User.user_id]
  recycler_id integer [ref: > RecyclerProfile.recycler_id]
  category_id integer [ref: > MasterWasteCategory.category_id]
  estimated_weight_kg decimal(8,2)
  waste_photo_url varchar
  address text
  latitude decimal(10,8)
  longitude decimal(11,8)
  proposed_time_slot timestamp
  confirmed_time_slot timestamp
  status request_status [default: 'Pending']
  created_at timestamp [default: `now()`]
  updated_at timestamp
}

Table ChatMessage {
  message_id integer [pk, increment]
  request_id integer [ref: > HandoverRequest.request_id]
  sender_id integer [ref: > User.user_id]
  message_text text
  attachment_url varchar
  sent_at timestamp [default: `now()`]
}

Table HandoverReceipt {
  receipt_id integer [pk, increment]
  request_id integer [unique, ref: - HandoverRequest.request_id]
  recycler_id integer [ref: > RecyclerProfile.recycler_id]
  user_id integer [ref: > User.user_id]
  actual_weight_kg decimal(8,2) [not null]
  total_reference_amount decimal(12,2)
  scale_proof_photo_url varchar
  created_at timestamp [default: `now()`]
  confirmed_by_user_at timestamp
}

Table RecyclerRating {
  rating_id integer [pk, increment]
  request_id integer [unique, ref: - HandoverRequest.request_id]
  user_id integer [ref: > User.user_id]
  recycler_id integer [ref: > RecyclerProfile.recycler_id]
  rating_stars integer [not null]
  comment text
  created_at timestamp [default: `now()`]
}

Table TransactionComplaint {
  complaint_id integer [pk, increment]
  request_id integer [ref: > HandoverRequest.request_id]
  complainant_id integer [ref: > User.user_id]
  issue_type varchar
  description text
  proof_photo_url varchar
  handled_by_staff_id integer [ref: > StaffProfile.staff_id]
  resolution_summary text
  status complaint_status [default: 'Pending']
  resolved_at timestamp
}

// ==========================================
// 4. PHÂN HỆ THANH TOÁN DỊCH VỤ NỘI BỘ (VIETQR)
// ==========================================

Table ServicePackage {
  package_id integer [pk, increment]
  package_name varchar [not null]
  target_role varchar
  fee_amount_vnd decimal(12,2) [not null]
  duration_days integer [not null]
  description text
  is_active boolean [default: true]
}

Table ServiceOrder {
  order_id integer [pk, increment]
  order_code varchar [not null, unique]
  user_id integer [ref: > User.user_id]
  package_id integer [ref: > ServicePackage.package_id]
  amount_vnd decimal(12,2) [not null]
  qr_payload text
  status order_status [default: 'Pending']
  paid_at timestamp
}

// ==========================================
// 5. PHÂN HỆ GAMIFICATION & QUẢN TRỊ HỆ THỐNG
// ==========================================

Table UserPointWallet {
  wallet_id integer [pk, increment]
  user_id integer [unique, ref: - User.user_id]
  total_points_balance integer [default: 0]
  updated_at timestamp
}

Table PointTransactionHistory {
  transaction_id integer [pk, increment]
  user_id integer [ref: > User.user_id]
  points_change integer [not null]
  action_type action_type [not null]
  reference_id integer
  created_at timestamp [default: `now()`]
}

Table DailyCheckinLog {
  checkin_id integer [pk, increment]
  user_id integer [ref: > User.user_id]
  checkin_date date [not null]
  streak_count integer [default: 1]
  points_awarded integer [default: 0]
}

Table VirtualTreeProgress {
  tree_id integer [pk, increment]
  user_id integer [unique, ref: - User.user_id]
  current_stage integer [default: 1]
  total_care_count integer [default: 0]
  daily_care_count integer [default: 0]
  last_care_date date
}

Table GiftInventory {
  gift_id integer [pk, increment]
  gift_name varchar [not null]
  gift_type gift_type [not null]
  points_required integer [not null]
  stock_quantity integer [default: 0]
  is_active boolean [default: true]
}

Table GiftRedemption {
  redemption_id integer [pk, increment]
  user_id integer [ref: > User.user_id]
  gift_id integer [ref: > GiftInventory.gift_id]
  points_spent integer [not null]
  voucher_code varchar
  status redemption_status [default: 'Redeemed']
  redeemed_at timestamp [default: `now()`]
}

Table EcoFact {
  fact_id integer [pk, increment]
  title varchar [not null]
  content text [not null]
  image_url varchar
  unlock_tree_stage integer [default: 1]
}

Table BlogPost {
  post_id integer [pk, increment]
  author_staff_id integer [ref: > StaffProfile.staff_id]
  title varchar [not null]
  content text [not null]
  cover_image_url varchar
  status post_status [default: 'Draft']
  approved_by_admin_id integer [ref: > User.user_id]
  published_at timestamp
}

Table Notification {
  notification_id integer [pk, increment]
  recipient_id integer [ref: > User.user_id]
  title varchar [not null]
  content text [not null]
  type varchar
  is_read boolean [default: false]
  created_at timestamp [default: `now()`]
}

Table AuditLog {
  log_id integer [pk, increment]
  actor_id integer [ref: > User.user_id]
  action_name varchar [not null]
  target_entity varchar
  record_id integer
  old_value text
  new_value text
  created_at timestamp [default: `now()`]
}

// ==========================================
// NHÓM BẢNG THEO MODULE (GOM CỤM TRỰC QUAN HÓA)
// ==========================================

TableGroup Auth_Module {
  User
  RecyclerProfile
  StaffProfile
  PasswordResetToken
}

TableGroup AI_Waste_Module {
  MasterWasteCategory
  SortingGuidanceRule
  AIScanHistory
  MisclassifiedReport
  VerifiedDataset
}

TableGroup Trade_Handover_Module {
  RecyclerPriceList
  HandoverRequest
  ChatMessage
  HandoverReceipt
  RecyclerRating
  TransactionComplaint
}

TableGroup Payment_Module {
  ServicePackage
  ServiceOrder
}

TableGroup Gamification_Admin_Module {
  UserPointWallet
  PointTransactionHistory
  DailyCheckinLog
  VirtualTreeProgress
  GiftInventory
  GiftRedemption
  EcoFact
  BlogPost
  Notification
  AuditLog
}