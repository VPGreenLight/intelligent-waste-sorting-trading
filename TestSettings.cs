// Các test Recycler và Handover cùng thay đổi bảng giá / lịch / trạng thái Online của tài khoản collector dùng chung,
// nên không được chạy song song giữa các class.
[assembly: CollectionBehavior(DisableTestParallelization = true)]
