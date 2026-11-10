import React, { useCallback, useEffect, useState } from "react";
import { api, ClassInfo, Hold } from "./api";
import { errorMessage } from "./messages";

const STUDENT_KEY = "studentId";
const POLL_MS = 3000;

function useInterval(fn: () => void, ms: number) {
  useEffect(() => {
    fn();
    const id = setInterval(fn, ms);
    return () => clearInterval(id);
  }, [fn, ms]);
}

export default function App() {
  const [tab, setTab] = useState<"admin" | "classes" | "mine">("classes");
  const [studentId, setStudentId] = useState(
    () => localStorage.getItem(STUDENT_KEY) ?? "",
  );
  const validId = /^\d{17}$/.test(studentId.trim());
  const sid = validId ? studentId.trim() : "";

  const changeStudent = (value: string) => {
    setStudentId(value);
    localStorage.setItem(STUDENT_KEY, value.trim());
  };

  return (
    <main className="container">
      <header className="hero">
        <h1>অন্যরকম স্কুল</h1>
        <div className="top">
          <label>
            জন্ম নিবন্ধন নম্বর
            <input
              inputMode="numeric"
              value={studentId}
              onChange={(e) => changeStudent(e.target.value)}
              placeholder="১৭ ডিজিটের জন্মনিবন্ধন নম্বর"
            />
          </label>
          {studentId && !validId && (
            <p className="error">নম্বরটি ১৭ ডিজিটের হতে হবে।</p>
          )}
        </div>
      </header>

      <nav className="tabs">
        <button
          className={tab === "admin" ? "active" : ""}
          onClick={() => setTab("admin")}
        >
          Class তৈরি (admin)
        </button>
        <button
          className={tab === "classes" ? "active" : ""}
          onClick={() => setTab("classes")}
        >
          Class তালিকা
        </button>
        <button
          className={tab === "mine" ? "active" : ""}
          onClick={() => setTab("mine")}
        >
          আমার আবেদন
        </button>
      </nav>

      {tab === "admin" && <AdminPage />}
      {tab === "classes" && <ClassesPage studentId={sid} />}
      {tab === "mine" && <MyHoldsPage studentId={sid} />}
    </main>
  );
}

function AdminPage() {
  const [name, setName] = useState("");
  const [seats, setSeats] = useState("30");
  const [msg, setMsg] = useState("");

  async function submit(e: React.FormEvent) {
    e.preventDefault();
    const total = Number(seats);
    if (!Number.isInteger(total) || total < 1) {
      setMsg("আসনের সংখ্যা ১ বা তার বেশি হতে হবে।");
      return;
    }
    try {
      const c = await api.createClass(name.trim(), total);
      setMsg(`"${c.name}" তৈরি হয়েছে।`);
      setName("");
    } catch (err) {
      setMsg(errorMessage(err));
    }
  }

  return (
    <section className="card">
      <h2>নতুন class</h2>
      <form onSubmit={submit} className="row">
        <input
          placeholder="যেমন: Class 6 – Morning Shift"
          value={name}
          onChange={(e) => setName(e.target.value)}
          required
        />
        <input
          type="number"
          inputMode="numeric"
          min={1}
          placeholder="আসন"
          value={seats}
          onChange={(e) => setSeats(e.target.value)}
          required
        />
        <button type="submit">তৈরি করুন</button>
      </form>
      {msg && <p className="notice">{msg}</p>}
    </section>
  );
}

function SeatBar({
  total,
  held,
  confirmed,
}: {
  total: number;
  held: number;
  confirmed: number;
}) {
  const available = total - held - confirmed;
  return (
    <div className="seatbar">
      <div className="track">
        <div
          className="fill confirmed"
          style={{ width: `${(confirmed / total) * 100}%` }}
        />
        <div
          className="fill held"
          style={{ width: `${(held / total) * 100}%` }}
        />
      </div>
      <small>{available} খালি</small>
    </div>
  );
}

function ClassesPage({ studentId }: { studentId: string }) {
  const [classes, setClasses] = useState<ClassInfo[]>([]);
  const [msg, setMsg] = useState("");

  const load = useCallback(async () => {
    try {
      setClasses(await api.listClasses());
    } catch (e) {
      setMsg(errorMessage(e));
    }
  }, []);
  useInterval(load, POLL_MS);

  async function apply(c: ClassInfo) {
    if (!studentId) {
      setMsg("প্রথমে উপরে ১৭ ডিজিটের জন্ম নিবন্ধন নম্বর দিন।");
      return;
    }
    try {
      const hold = await api.applyHold(c.id, studentId);
      const time = new Date(hold.expiresAt).toLocaleTimeString("bn-BD");
      setMsg(`"${c.name}"-এ seat রাখা হয়েছে। ${time}-এর মধ্যে ভর্তি ফি দিন।`);
      await load();
    } catch (e) {
      setMsg(errorMessage(e));
    }
  }

  return (
    <section className="card">
      <h2>Class তালিকা</h2>
      {msg && <p className="notice">{msg}</p>}
      <table>
        <thead>
          <tr>
            <th>Class</th>
            <th>মোট</th>
            <th>আবেদন (hold)</th>
            <th>নিশ্চিত</th>
            <th>খালি আসন</th>
            <th></th>
          </tr>
        </thead>
        <tbody>
          {classes.map((c) => (
            <tr key={c.id}>
              <td>{c.name}</td>
              <td>{c.totalSeats}</td>
              <td>{c.heldSeats}</td>
              <td>{c.confirmedSeats}</td>
              <td>
                <SeatBar
                  total={c.totalSeats}
                  held={c.heldSeats}
                  confirmed={c.confirmedSeats}
                />
              </td>
              <td>
                <button
                  onClick={() => apply(c)}
                  disabled={c.availableSeats <= 0}
                >
                  আবেদন করুন
                </button>
              </td>
            </tr>
          ))}
        </tbody>
      </table>
    </section>
  );
}

function MyHoldsPage({ studentId }: { studentId: string }) {
  const [holds, setHolds] = useState<Hold[]>([]);
  const [msg, setMsg] = useState("");

  const load = useCallback(async () => {
    if (!studentId) return;
    try {
      setHolds(await api.myHolds(studentId));
    } catch (e) {
      setMsg(errorMessage(e));
    }
  }, [studentId]);
  useInterval(load, POLL_MS);

  if (!studentId)
    return (
      <section className="card">
        <p className="empty">উপরে ১৭ ডিজিটের জন্ম নিবন্ধন নম্বর দিন।</p>
      </section>
    );

  return (
    <section className="card">
      <h2>আমার আবেদন</h2>
      {msg && <p className="error">{msg}</p>}
      {holds.length === 0 && <p className="empty">কোনো আবেদন নেই।</p>}
      {holds.map((h) => (
        <HoldRow
          key={h.holdId}
          hold={h}
          studentId={studentId}
          onChanged={load}
        />
      ))}
    </section>
  );
}

const STATUS_TEXT: Record<string, string> = {
  HELD: "ভর্তি ফি বাকি",
  CONFIRMED: "ভর্তি নিশ্চিত",
  EXPIRED: "সময় শেষ",
  CANCELLED: "বাতিল",
};

function HoldRow({
  hold,
  studentId,
  onChanged,
}: {
  hold: Hold;
  studentId: string;
  onChanged: () => void;
}) {
  const [now, setNow] = useState(Date.now());
  const [err, setErr] = useState("");

  useEffect(() => {
    if (hold.status !== "HELD") return;
    const id = setInterval(() => setNow(Date.now()), 1000);
    return () => clearInterval(id);
  }, [hold.status]);

  const remaining = Math.max(
    0,
    Math.floor((new Date(hold.expiresAt).getTime() - now) / 1000),
  );
  const expiredLocally = hold.status === "HELD" && remaining === 0;
  const status = expiredLocally ? "EXPIRED" : hold.status;

  // Time ran out while the page was open: re-sync with the server without a reload.
  useEffect(() => {
    if (expiredLocally) onChanged();
  }, [expiredLocally, onChanged]);

  async function confirm() {
    try {
      await api.confirm(hold.holdId, studentId);
      setErr("");
      onChanged();
    } catch (e) {
      setErr(errorMessage(e));
    }
  }

  async function cancel() {
    try {
      await api.cancel(hold.holdId, studentId);
      setErr("");
      onChanged();
    } catch (e) {
      setErr(errorMessage(e));
    }
  }

  const mm = String(Math.floor(remaining / 60)).padStart(2, "0");
  const ss = String(remaining % 60).padStart(2, "0");

  return (
    <div className={`hold ${status.toLowerCase()}`}>
      <div>
        <strong>{hold.className}</strong>
        <span className={`badge badge-${status.toLowerCase()}`}>
          {STATUS_TEXT[status] ?? status}
        </span>
        {status === "HELD" && (
          <span className="countdown">
            {mm}:{ss}
          </span>
        )}
      </div>
      {status === "HELD" && (
        <div className="row">
          <button onClick={confirm}>ভর্তি ফি দিন</button>
          <button onClick={cancel} className="danger">
            বাতিল
          </button>
        </div>
      )}
      {err && <p className="error">{err}</p>}
    </div>
  );
}
