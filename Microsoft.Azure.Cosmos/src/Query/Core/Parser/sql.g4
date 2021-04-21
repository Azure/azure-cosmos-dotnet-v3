grammar sql;

program
	: sql_query EOF
	;

sql_query : select_clause from_clause? where_clause? group_by_clause? order_by_clause? offset_limit_clause? ;

/*--------------------------------------------------------------------------------*/
/* SELECT */
/*--------------------------------------------------------------------------------*/
select_clause : K_SELECT K_DISTINCT? top_spec? selection ;
top_spec : K_TOP (NUMERIC_LITERAL | PARAMETER);
selection
	: select_star_spec
	| select_value_spec 
	| select_list_spec
	;
select_star_spec : '*' ;
select_value_spec : K_VALUE scalar_expression ;
select_list_spec : select_item ( ',' select_item )* ;
select_item : scalar_expression (K_AS IDENTIFIER)? ;
/*--------------------------------------------------------------------------------*/

/*--------------------------------------------------------------------------------*/
/* FROM */
/*--------------------------------------------------------------------------------*/
from_clause : K_FROM collection_expression ;
collection_expression
	: collection (K_AS IDENTIFIER)? #AliasedCollectionExpression
	| IDENTIFIER K_IN collection #ArrayIteratorCollectionExpression 
	| collection_expression K_JOIN collection_expression #JoinCollectionExpression
	;
collection
	: IDENTIFIER path_expression?  #InputPathCollection
	| '(' sql_query ')' #SubqueryCollection
	;
path_expression
	: path_expression'.'IDENTIFIER #IdentifierPathExpression
	| path_expression'[' NUMERIC_LITERAL ']' #NumberPathExpression
	| path_expression'[' STRING_LITERAL ']' #StringPathExpression
	| /*epsilon*/ #EpsilonPathExpression
	;
/*--------------------------------------------------------------------------------*/

/*--------------------------------------------------------------------------------*/
/* WHERE */
/*--------------------------------------------------------------------------------*/
where_clause : K_WHERE scalar_expression ;
/*--------------------------------------------------------------------------------*/

/*--------------------------------------------------------------------------------*/
/* GROUP BY */
/*--------------------------------------------------------------------------------*/
group_by_clause : K_GROUP K_BY scalar_expression_list ;
/*--------------------------------------------------------------------------------*/

/*--------------------------------------------------------------------------------*/
/* ORDER BY */
/*--------------------------------------------------------------------------------*/
order_by_clause : K_ORDER K_BY order_by_items ;
order_by_items : order_by_item (',' order_by_item)* ;
order_by_item : scalar_expression sort_order? ;
sort_order
	: K_ASC
	| K_DESC
	;
/*--------------------------------------------------------------------------------*/

/*--------------------------------------------------------------------------------*/
/* OFFSET LIMIT */
/*--------------------------------------------------------------------------------*/
offset_limit_clause : K_OFFSET offset_count K_LIMIT limit_count;
offset_count : NUMERIC_LITERAL | PARAMETER;
limit_count : NUMERIC_LITERAL | PARAMETER;
/*--------------------------------------------------------------------------------*/

/*--------------------------------------------------------------------------------*/
/* SCALAR EXPRESSIONs */
/*--------------------------------------------------------------------------------*/
scalar_expression
	: scalar_expression '?' scalar_expression ':' scalar_expression #ConditionalScalarExpression
	| scalar_expression '??' scalar_expression #CoalesceScalarExpression
	| logical_scalar_expression #LogicalScalarExpression
	| binary_scalar_expression K_NOT? K_BETWEEN binary_scalar_expression K_AND binary_scalar_expression #BetweenScalarExpression
	;

logical_scalar_expression
	: binary_scalar_expression
	| in_scalar_expression
	| like_scalar_expression
	;

in_scalar_expression
	: binary_scalar_expression K_NOT? K_IN '(' scalar_expression_list ')'
	;

like_scalar_expression
	: binary_scalar_expression K_NOT? K_LIKE binary_scalar_expression escape_expression?
	;

escape_expression
	: K_ESCAPE STRING_LITERAL
	;

binary_scalar_expression
	: unary_scalar_expression
	| binary_scalar_expression multiplicative_operator binary_scalar_expression
	| binary_scalar_expression additive_operator binary_scalar_expression
	| binary_scalar_expression relational_operator binary_scalar_expression
	| binary_scalar_expression equality_operator binary_scalar_expression
	| binary_scalar_expression bitwise_and_operator binary_scalar_expression
	| binary_scalar_expression bitwise_exclusive_or_operator binary_scalar_expression
	| binary_scalar_expression bitwise_inclusive_or_operator binary_scalar_expression
	| binary_scalar_expression K_AND binary_scalar_expression
	| binary_scalar_expression K_OR binary_scalar_expression
	| binary_scalar_expression string_concat_operator binary_scalar_expression
	;

multiplicative_operator
	: '*' 
	| '/' 
	| '%' 
	;

additive_operator
	: '+' 
	| '-'
	;

relational_operator
	: '<'
	| '>' 
	| '>=' 
	| '<='
	;

equality_operator
	: '=' 
	| '!=' 
	;

bitwise_and_operator : '&' ;

bitwise_exclusive_or_operator : '^';

bitwise_inclusive_or_operator : '|';

string_concat_operator : '||';

unary_scalar_expression
	: primary_expression
	| unary_operator unary_scalar_expression
	;

unary_operator
	: '-' 
	| '+' 
	| '~' 
	| K_NOT
	;

primary_expression
	: IDENTIFIER #PropertyRefScalarExpressionBase
	| PARAMETER #ParameterRefScalarExpression
	| literal #LiteralScalarExpression
	| '[' scalar_expression_list? ']' #ArrayCreateScalarExpression
	| '{' object_property_list? '}' #ObjectCreateScalarExpression
	| (K_UDF '.')? IDENTIFIER '(' scalar_expression_list? ')' #FunctionCallScalarExpression
	| '(' scalar_expression ')' #ParenthesizedScalarExperession
	| '(' sql_query ')' #SubqueryScalarExpression
	| primary_expression '.' IDENTIFIER #PropertyRefScalarExpressionRecursive
	| primary_expression '[' scalar_expression  ']' #MemberIndexerScalarExpression
	| K_EXISTS '(' sql_query ')' #ExistsScalarExpression
	| K_ARRAY '(' sql_query ')' #ArrayScalarExpression
	;

scalar_expression_list : scalar_expression (',' scalar_expression)*;

object_property_list : object_property (',' object_property)* ;

object_property : STRING_LITERAL ':' scalar_expression ;
/*--------------------------------------------------------------------------------*/

/*--------------------------------------------------------------------------------*/
/* KEYWORDS */
/*--------------------------------------------------------------------------------*/
K_AND : A N D;
K_ARRAY : A R R A Y;
K_AS : A S;
K_ASC : A S C;
K_BETWEEN : B E T W E E N;
K_BY : B Y;
K_DESC : D E S C;
K_DISTINCT : D I S T I N C T;
K_ESCAPE: E S C A P E;
K_EXISTS : E X I S T S;
K_FALSE : 'false';
K_FROM : F R O M;
K_GROUP : G R O U P;
K_IN : I N ;
K_JOIN : J O I N;
K_LIKE : L I K E;
K_LIMIT : L I M I T;
K_NOT : N O T;
K_NULL : 'null';
K_OFFSET : O F F S E T;
K_OR : O R;
K_ORDER : O R D E R;
K_SELECT : S E L E C T;
K_TOP : T O P;
K_TRUE : 'true';
K_UDF : U D F;
K_UNDEFINED : 'undefined';
K_VALUE : V A L U E;
K_WHERE : W H E R E;
/*--------------------------------------------------------------------------------*/

WS
   : [ \r\n\t] + -> skip
   ;

/*--------------------------------------------------------------------------------*/
/* LITERALS */
/*--------------------------------------------------------------------------------*/
literal
	: STRING_LITERAL
	| NUMERIC_LITERAL
	| K_TRUE
	| K_FALSE
	| K_NULL
	| K_UNDEFINED
	;

NUMERIC_LITERAL
	: ( '+' | '-' )? DIGIT+ ( '.' DIGIT* )? ( E [-+]? DIGIT+ )?
	| ( '+' | '-' )? '.' DIGIT+ ( E [-+]? DIGIT+ )?
	;

STRING_LITERAL
	: '"' (ESC | SAFECODEPOINT)* '"'
	| '\'' (ESC | SAFECODEPOINT)* '\''
	;

fragment ESC
	: '\\' (["\\/bfnrt] | UNICODE)
	;

fragment UNICODE
   : 'u' HEX HEX HEX HEX
   ;

fragment HEX
   : [0-9a-fA-F]
   ;

fragment SAFECODEPOINT
   : ~ ["\\\u0000-\u001F]
   ;

IDENTIFIER
	: 
	| [a-zA-Z_]([a-zA-Z_]|DIGIT)*
	;

PARAMETER
	: '@'IDENTIFIER
	;
/*--------------------------------------------------------------------------------*/

/*--------------------------------------------------------------------------------*/
/* FRAGMENTS */
/*--------------------------------------------------------------------------------*/
fragment DIGIT : [0-9];

fragment A : [aA];
fragment B : [bB];
fragment C : [cC];
fragment D : [dD];
fragment E : [eE];
fragment F : [fF];
fragment G : [gG];
fragment H : [hH];
fragment I : [iI];
fragment J : [jJ];
fragment K : [kK];
fragment L : [lL];
fragment M : [mM];
fragment N : [nN];
fragment O : [oO];
fragment P : [pP];
fragment Q : [qQ];
fragment R : [rR];
fragment S : [sS];
fragment T : [tT];
fragment U : [uU];
fragment V : [vV];
fragment W : [wW];
fragment X : [xX];
fragment Y : [yY];
fragment Z : [zZ];
/*--------------------------------------------------------------------------------*/