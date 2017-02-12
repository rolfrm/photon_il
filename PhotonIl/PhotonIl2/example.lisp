;; 
(i32 5)
(f32 3.14)
(+ (i32 5) (i32 5))
(+ (f32 3.41) (f32 1.1111))
(System.Console.WriteLine (str example.lisp))

(defun addtwo ((x i32) (y i32) i32)
	(+ (+ x y) (i32 10)))

;(defun addtwo ((x i32) (y i32 5) i32)
;	(+ x y))

(addtwo (i32 3) (i32 5))
(addtwo (addtwo (i32 3) (i32 5)) (i32 10))

(defun addthree ((x i32) (y i32) (z i32) i32)
	(+ (+ x y) z))

(defun addf ((x f32) (y f32) f32)
	(+ x y))

(addthree (i32 1) (i32 2) (i32 3))
(addf (f32 1.23) (f32 3.41))
(+ (f32 1.1) (f32 2.2) (f32 3.3))

(defvar p f32)
(setvar p (f32 1.2))
p
(+ p p)
(+ p p p)

"
(defstruct vec2
  (x i32)
  (y i32))

 (defvar p (make-vec2 :x (f32 1.0) :y (f32 2.0)))
 "
(str "Yay all works!")

;(box 5)
;(new System.String 5)

